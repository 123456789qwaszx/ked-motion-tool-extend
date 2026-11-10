using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

/// <summary>
/// 예시 문서(<c>examples/emoji</c>)가 살아 있는지 — 문제 없이 서고, 저장된 파일이 지은 것과 같고, 그림이 다 있다.
/// 예시를 고쳤으면 <c>MT_WRITE_EXAMPLES=1 dotnet test --filter EmojiExampleTests</c>로 파일을 다시 쓴다.
/// </summary>
public class EmojiExampleTests
{
    private static readonly RigSchemaFile Dump = RigSchemaFile.Load(Fixtures.PathOf("rig-schemas.json"));

    private static string ExampleDirectory()
    {
        DirectoryInfo? at = new(AppContext.BaseDirectory);

        while (at is not null && !File.Exists(Path.Combine(at.FullName, "KedMotionTool.sln")))
        {
            at = at.Parent;
        }

        return Path.Combine(at?.FullName ?? throw new InvalidOperationException("저장소 뿌리를 못 찾았다"), "examples", "emoji");
    }

    [Fact]
    public void 예시_문서는_문제_없이_서고_저장된_파일과_같다()
    {
        MotionEditor editor = EmojiExamples.Build();
        MotionDocument document = editor.Document;
        RigSchemaFile rig = RigAssets.Compose(Dump, document);

        Assert.True(!MotionValidator.Validate(document, rig.NodeIds, null, rig.AlphaNodeIds).Any(), string.Join("\n", MotionValidator.Validate(document, rig.NodeIds, null, rig.AlphaNodeIds).Select(p => $"{p.MotionName}: {p.Message}")));
        Assert.Empty(EmojiValidator.Validate(document, rig.NodeIds).Select(p => $"{p.EmojiName}: {p.Message}"));
        Assert.Empty(ClipValidator.Validate(document, rig.NodeIds).Select(p => p.Message));
        Assert.Equal(22, document.Emojis.Count);

        string directory = ExampleDirectory();
        string path = Path.Combine(directory, "emoji-examples.motion-authoring.json");
        string json = MotionDocumentJson.Write(document);

        if (Environment.GetEnvironmentVariable("MT_WRITE_EXAMPLES") == "1")
        {
            File.WriteAllText(path, json);
        }

        Assert.Equal(json, File.ReadAllText(path));

        // 스킨이 가리키는 그림이 다 있다.
        Assert.All(document.Skins.SelectMany(s => s.AllImages()).Distinct(), image => Assert.True(File.Exists(Path.Combine(directory, image)), image));
    }

    [Fact]
    public void 예시_클립에서_이벤트가_이모지를_띄우고_끈다()
    {
        MotionEditor editor = EmojiExamples.Build();
        MotionDocument document = editor.Document;
        ClipAsset clip = document.FindClip("emoji_demo")!;
        string? Playing(double t)
            => EmojiEvents.Resolve(document, document.ActiveSceneAsset!, ClipPreview.EventsBetween(document, clip, double.NegativeInfinity, t), t).SingleOrDefault().Emoji?.Name;

        Assert.Null(Playing(0.2));
        Assert.Equal("빨간경고막대기", Playing(0.6));
        Assert.Null(Playing(1.9));              // 잠깐 뜨고 사라졌다
        Assert.Equal("행복한벚꽃", Playing(3.5)); // 머문다
        Assert.Equal("행복한벚꽃", Playing(4.6)); // 끄라는 말이 왔다 — 퇴장 중
        Assert.Null(Playing(5.0));
        Assert.Equal("말줄임점풍선", Playing(8.0));
        Assert.Null(Playing(9.5));
    }

    // ── 화면 이동 연출 예시(PLAN-set.md S6) ────────────────────────────

    private static string StationDirectory() => Path.Combine(Path.GetDirectoryName(ExampleDirectory())!, "transitions", "station");

    [Fact]
    public void 화면_이동_예시는_층_표에서_세트가_서고_문제_없이_돌고_저장된_파일과_같다()
    {
        string directory = StationDirectory();
        MotionEditor editor = TransitionExamples.BuildStation(Path.Combine(directory, "art", "layers.tsv"));
        MotionDocument document = editor.Document;
        RigSchemaFile rig = RigAssets.Compose(Dump, document);

        // 층 표: 바탕색 + 층 열 장이 오브젝트 열하나로 — 뒤에서 앞으로.
        RigAsset set = document.FindRig(TransitionExamples.Station)!;
        Assert.Equal(new[] { "Backdrop", "arches", "floor", "pillars_left", "pillars_right", "train", "sign", "light", "microbe_sun", "microbe_leaf", "microbe_cell" }, set.Nodes.Select(n => n.Id));
        Assert.Equal(("#10262B", 1920f, 1080f), (set.Find("Backdrop")!.Color, set.Find("Backdrop")!.Width, set.Find("Backdrop")!.Height));
        RigNodeAsset sign = set.Find("sign")!; // 표: 왼쪽 556 · 위 56 · 889×249 → 가운데 기준 (40.5, 359.5)
        Assert.Equal((40.5f, 359.5f, 889f, 249f), (sign.X, sign.Y, sign.Width, sign.Height));

        string Problems<T>(IEnumerable<T> problems, Func<T, string> text) => string.Join("\n", problems.Select(text));
        Assert.True(!MotionValidator.Validate(document, rig.NodeIds, null, rig.AlphaNodeIds).Any(), Problems(MotionValidator.Validate(document, rig.NodeIds, null, rig.AlphaNodeIds), p => $"{p.MotionName}: {p.Message}"));
        Assert.True(!EmojiValidator.Validate(document, rig.NodeIds).Any(), Problems(EmojiValidator.Validate(document, rig.NodeIds), p => $"{p.EmojiName}: {p.Message}"));

        string path = Path.Combine(directory, "station.motion-authoring.json");
        string json = MotionDocumentJson.Write(document);

        if (Environment.GetEnvironmentVariable("MT_WRITE_EXAMPLES") == "1")
        {
            File.WriteAllText(path, json);
        }

        Assert.Equal(json, File.ReadAllText(path));
        Assert.All(document.Skins.SelectMany(s => s.AllImages()).Distinct(), image => Assert.True(File.Exists(Path.Combine(directory, image)), image));

        // 수명: 등장 1.2 › 머묾 1 › 퇴장 0.9. 등장의 가운데에서 왼쪽 기둥은 아직 들어오는 중이고, 끝나면 제자리다.
        EmojiAsset cue = document.FindEmoji(TransitionExamples.Station)!;
        Assert.Equal(3.1f, EmojiPreview.PhasesOf(document, cue).Total, 3);
        var scene = new SceneAsset { Name = "s", Objects = { new SceneObject { Id = "t", Rig = TransitionExamples.Station } } };
        RectNodeTree template = ScenePreview.Build(rig, scene);
        float rest = template.GetState("t/pillars_left").AnchoredPosition.X;
        float PillarX(double t) => EmojiPreview.Pose(template, document, cue, scene.Objects[0], t).GetState("t/pillars_left").AnchoredPosition.X;
        Assert.Equal(rest - 820f, PillarX(0.1), 1);
        Assert.InRange(PillarX(0.6), rest - 700f, rest - 100f);
        Assert.Equal(rest, PillarX(1.2), 1);
        Assert.Equal(rest, PillarX(2.0), 1);          // 머묾 — 기둥은 가만히
        Assert.True(PillarX(2.9) < rest - 400f);      // 퇴장 — 들어온 길로 물러난다

        // "covered" 이벤트가 등장의 0.25초에 — 대본이 여기서 장면을 바꾼다.
        MotionEventAsset covered = Assert.Single(document.Find("station_in")!.Events);
        Assert.Equal(("covered", 0.25f), (covered.Name, covered.T));
    }
}
