using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ked.Presentation.Core;
using Mt.Authoring;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;

namespace Mt.Cli;

/// <summary>
/// <c>mt</c> — 툴의 명령줄 입구. 창 없이 저작 원본(<c>*.motion-authoring.json</c>)을 읽어 검증 · 컷 내보내기 · 포즈 조회를 한다.
///
/// 설계 원칙: **결과는 표준 출력에 JSON 하나**(기계가 읽는다), 사람에게 하는 말은 표준 오류, 종료 코드는 0 = 됨 · 1 = 막는 문제가 있다 · 2 = 쓰는 법이 틀렸다.
/// 화면과 같은 함수를 지난다(<see cref="MotionValidator"/> · <see cref="CutFileJson"/> · <see cref="MotionPreview"/>) —
/// 여기서 통과한 것은 앱에서도 통과하고, 여기서 읽은 포즈는 뷰포트가 보이는 포즈다.
/// </summary>
public static class MtCli
{
    public const string Usage = """
        mt — ked-motion-tool 명령줄 (결과는 stdout의 JSON, 종료 코드 0 됨 · 1 막는 문제 · 2 쓰는 법)

          mt info     <문서>                              애니메이션·스킨·컷·세트의 목록
          mt validate <문서>                              문제 목록. 막는 문제가 있으면 종료 코드 1
          mt cut      <문서> <컷 이름> -o <파일>           컷 파일(cut/1) 내보내기 — 그 컷에 막는 문제가 없어야 나간다
          mt pose     <문서> --motion <이름> --time <초>  그 시각의 포즈 — 그 애니메이션이 얹히는 세트·컷에서 그것이 건드리는 오브젝트의 값, 불투명도, 방금 울린 이벤트

          <문서>  *.motion-authoring.json
        """;

    private static readonly JsonSerializerOptions Output = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            stdout.WriteLine(Usage);
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            var options = Options.Parse(args.Skip(1).ToArray());

            return args[0] switch
            {
                "info" => Info(options, stdout),
                "validate" => Validate(options, stdout),
                "cut" => Cut(options, stdout),
                "pose" => Pose(options, stdout),
                _ => throw new UsageException($"모르는 명령 '{args[0]}'"),
            };
        }
        catch (UsageException e)
        {
            stderr.WriteLine($"mt: {e.Message}");
            stderr.WriteLine(Usage);
            return 2;
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException)
        {
            // 없는 파일·깨진 JSON·없는 이름 — 기계도 읽을 수 있게 JSON으로도 남긴다.
            stderr.WriteLine($"mt: {e.Message}");
            Write(stdout, new JsonObject { ["ok"] = false, ["error"] = e.Message });
            return 1;
        }
    }

    // ── 명령 ──────────────────────────────────────────────────────────

    private static int Info(Options options, TextWriter stdout)
    {
        MotionDocument document = Load(options, out _);
        var motions = new JsonArray();

        foreach (MotionAsset m in document.Motions)
        {
            motions.Add(new JsonObject
            {
                ["name"] = m.Name,
                ["rig"] = m.Rig,
                ["duration"] = m.Duration,
                ["channels"] = new JsonArray(m.Channels.Select(c => (JsonNode)$"{c.Node} {ChannelKindLabel.Of(c.Kind)}").ToArray()),
                ["events"] = m.Events.Count,
            });
        }

        Write(stdout, new JsonObject
        {
            ["ok"] = true,
            ["schema"] = document.Schema,
            ["motions"] = motions,
            ["skins"] = new JsonArray(document.Skins.Select(s => (JsonNode)new JsonObject { ["name"] = s.Name, ["rig"] = s.Rig, ["parts"] = s.Parts.Count }).ToArray()),
            ["cuts"] = new JsonArray(document.Cuts.Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["frames"] = c.Frames, ["objects"] = c.Nodes.Count }).ToArray()),
            ["sets"] = new JsonArray(document.Rigs.Select(r => (JsonNode)new JsonObject { ["name"] = r.Kind, ["objects"] = r.Nodes.Count }).ToArray()),
        });
        return 0;
    }

    private static int Validate(Options options, TextWriter stdout)
    {
        MotionDocument document = Load(options, out RigSchemaFile rig);
        (JsonArray problems, bool blocking) = Problems(document, rig);
        Write(stdout, new JsonObject { ["ok"] = !blocking, ["problems"] = problems });
        return blocking ? 1 : 0;
    }

    private static int Cut(Options options, TextWriter stdout)
    {
        string cutName = options.Positional(1, "컷 이름");
        string target = options.Value("o") ?? throw new UsageException("cut에는 -o <파일>이 있어야 한다");
        MotionDocument document = Load(options, out RigSchemaFile rig);
        CutAsset cut = document.FindCut(cutName) ?? throw new KeyNotFoundException($"컷 '{cutName}'이 없다.");

        // 앱의 "컷 내보내기"와 같은 문을 지난다.
        List<MotionProblem> blocking = MotionValidator.BlockingCutExport(document, cutName, rig.NodeIds, rig.AlphaNodeIds);

        if (blocking.Count > 0)
        {
            Write(stdout, new JsonObject
            {
                ["ok"] = false,
                ["problems"] = new JsonArray(blocking.Select(p => (JsonNode)new JsonObject { ["name"] = p.MotionName, ["message"] = p.Message, ["blocking"] = true }).ToArray()),
            });
            return 1;
        }

        // 그림 경로는 컷 파일 기준으로 적는다(문서 기준 → 파일 기준). 그림은 복사하지 않는다.
        string documentFolder = Path.GetDirectoryName(Path.GetFullPath(options.Positional(0, "문서 경로"))) ?? ".";
        string folder = Path.GetDirectoryName(Path.GetFullPath(target)) ?? ".";
        CutFileDto file = CutFileJson.Build(document, cut, rig.Dto.stageSize.ToVec2(),
            image => Path.GetRelativePath(folder, Path.GetFullPath(Path.Combine(documentFolder, image))).Replace('\\', '/'));
        File.WriteAllBytes(target, new System.Text.UTF8Encoding(false).GetBytes(CutFileJson.Write(file)));
        Write(stdout, new JsonObject
        {
            ["ok"] = true,
            ["path"] = Path.GetFullPath(target),
            ["format"] = file.format,
            ["cut"] = file.name,
            ["frames"] = file.frames,
            ["objects"] = file.objects.Count,
            ["tracks"] = file.tracks.Count + file.images.Count,
            ["events"] = file.events.Count,
        });
        return 0;
    }

    private static int Pose(Options options, TextWriter stdout)
    {
        MotionDocument document = Load(options, out RigSchemaFile rig);
        double time = options.Number("time");
        var alphas = new Dictionary<string, float>(StringComparer.Ordinal);
        var result = new JsonObject { ["ok"] = true, ["time"] = time };

        string motionName = options.Value("motion") ?? throw new UsageException("pose에는 --motion이 있어야 한다");
        MotionAsset motion = document.Find(motionName) ?? throw new KeyNotFoundException($"모션 '{motionName}'이 없다.");
        result["motion"] = motion.Name;
        result["duration"] = motion.Duration;

        if (!rig.TryGetRig(motion.Rig, out _))
        {
            throw new InvalidOperationException($"모션 '{motion.Name}'이 얹히는 세트나 컷 '{motion.Rig}'가 없다.");
        }

        RectNodeTree pose = PoseOf(document, rig, motion, time, alphas);
        var nodes = new JsonObject();

        foreach (ChannelAsset channel in motion.Channels)
        {
            AddNodeValue(nodes, pose, alphas, channel);
        }

        result["rig"] = motion.Rig;
        result["nodes"] = nodes;

        result["events"] = new JsonArray(MotionEvents.Between(motion, time - EventWindow, time)
            .Select(e => (JsonNode)new JsonObject { ["time"] = e.T, ["name"] = e.Name, ["value"] = e.Value }).ToArray());

        Write(stdout, result);
        return 0;
    }

    // ── helper ────────────────────────────────────────────────────────

    /// <summary>pose가 "방금 울린 이벤트"로 치는 구간(초) — 앱의 뷰포트와 같다.</summary>
    private const double EventWindow = 0.4;

    private static (JsonArray Problems, bool Blocking) Problems(MotionDocument document, RigSchemaFile rig)
    {
        var problems = new JsonArray();
        bool blocking = false;

        foreach (MotionProblem p in MotionValidator.Validate(document, rig.NodeIds, rig.AlphaNodeIds))
        {
            problems.Add(new JsonObject { ["kind"] = "motion", ["name"] = p.MotionName, ["message"] = p.Message, ["blocking"] = p.Blocking });
            blocking |= p.Blocking;
        }

        return (problems, blocking);
    }

    /// <summary>앱의 뷰포트와 같은 조립 — 세트는 무대 가운데에 그대로 서고, 컷은 포즈를 잡은 뒤 카메라와 깊이(내장 깊이 커브)를 넣는다.</summary>
    private static RectNodeTree PoseOf(MotionDocument document, RigSchemaFile rig, MotionAsset motion, double time, Dictionary<string, float> alphas)
    {
        if (document.FindCut(motion.Rig) is { } cut && CutStage.Build(rig, cut, "") is { } staged)
        {
            RectNodeTree posed = MotionPreview.Pose(staged, motion, time, null, "", alphas);
            CutStage.Apply(posed, cut, "", BuiltinTuning.Depth);
            return posed;
        }

        return MotionPreview.Pose(rig.BuildTree(motion.Rig), motion, time, null, "", alphas);
    }

    private static void AddNodeValue(JsonObject nodes, RectNodeTree pose, Dictionary<string, float> alphas, ChannelAsset channel)
    {
        string key = channel.Node;

        if (!pose.Contains(key))
        {
            return;
        }

        var entry = nodes[channel.Node] as JsonObject ?? new JsonObject();
        nodes[channel.Node] = entry;
        RectNodeState state = pose.GetState(key);

        switch (channel.Kind)
        {
            case MotionChannelKind.Position:
                entry["position"] = new JsonArray(Round(state.AnchoredPosition.X), Round(state.AnchoredPosition.Y));
                break;
            case MotionChannelKind.Scale:
                entry["scale"] = new JsonArray(Round(state.LocalScale.X), Round(state.LocalScale.Y));
                break;
            case MotionChannelKind.Euler:
                entry["euler"] = new JsonArray(Round(state.LocalEulerAngles.X), Round(state.LocalEulerAngles.Y), Round(state.LocalEulerAngles.Z));
                break;
            case MotionChannelKind.Alpha:
                entry["alpha"] = Round(alphas.TryGetValue(key, out float alpha) ? alpha : 1f);
                break;
        }
    }

    /// <summary>읽는 쪽이 0.30000001을 보지 않게 — 소수 넷째 자리까지.</summary>
    private static double Round(float value) => Math.Round(value, 4);

    private static MotionDocument Load(Options options, out RigSchemaFile rig)
    {
        string path = options.Positional(0, "문서 경로");
        MotionDocument document = MotionDocumentJson.Read(File.ReadAllText(path));

        // 문서가 정의한 세트·컷으로 한 벌을 세운다(무대는 1920×1080).
        rig = RigAssets.Compose(document, BaseResolutionFile.Default);

        // 손으로(또는 AI가) 쓴 문서는 탄젠트 값을 모른다 — 키의 탄젠트 종류(auto·flat·linear…)대로 값을 세운다. 앱이 편집할 때마다 하는 그 계산이고,
        // 앱이 저장한 문서에는 아무것도 안 바꾼다(이미 그 값이다). free는 적힌 값 그대로 둔다.
        foreach (ComponentCurve curve in document.Motions.SelectMany(m => m.Channels).SelectMany(c => new[] { c.X, c.Y, c.Z }).OfType<ComponentCurve>())
        {
            Mt.Authoring.Graph.TangentMath.Recompute(curve.Keys);
        }

        return document;
    }

    private static void Write(TextWriter stdout, JsonNode node) => stdout.WriteLine(node.ToJsonString(Output));

    private sealed class UsageException : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }

    /// <summary>위치 인자와 <c>--이름 값</c> 옵션(<c>-o 값</c>도 받는다).</summary>
    private sealed class Options
    {
        private readonly List<string> _positional = new();
        private readonly Dictionary<string, string> _named = new(StringComparer.Ordinal);

        public static Options Parse(string[] args)
        {
            var options = new Options();

            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal) && args[i] != "-o")
                {
                    options._positional.Add(args[i]);
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    throw new UsageException($"{args[i]}에 값이 없다");
                }

                options._named[args[i].TrimStart('-')] = args[++i];
            }

            return options;
        }

        public string Positional(int index, string what)
            => index < _positional.Count ? _positional[index] : throw new UsageException($"{what}가 없다");

        public string? Value(string name) => _named.GetValueOrDefault(name);

        public string Required(string name) => Value(name) ?? throw new UsageException($"--{name}이 있어야 한다");

        public double Number(string name)
            => double.TryParse(Required(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : throw new UsageException($"--{name}은 수여야 한다 ('{Value(name)}')");
    }
}
