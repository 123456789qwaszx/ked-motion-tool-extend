using System.Text.Json.Nodes;
using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Serialization;
using Mt.Cli;

namespace Mt.Authoring.Tests;

/// <summary><c>mt</c> 명령줄 — 창 없이 검증 · 컷 내보내기 · 포즈. 결과는 stdout의 JSON, 종료 코드 0·1·2.</summary>
public class MtCliTests
{
    private static (int Code, JsonNode? Json, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int code = MtCli.Run(args, stdout, stderr);
        string text = stdout.ToString();
        JsonNode? json = null;

        try
        {
            json = JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            // 쓰는 법 안내처럼 JSON이 아닌 출력
        }

        return (code, json, text, stderr.ToString());
    }

    /// <summary>세트 actor와 그 애니메이션 nod·bow + nod에 이벤트 하나 + 세트 하나 더(prop). 임시 폴더에 저장한다.</summary>
    private static string WriteDocument(Action<DocumentEditor>? more = null)
    {
        var editor = new DocumentEditor(Fixtures.ActorDocument());
        editor.AddEventKey("nod", 0.3f, "flash", "땀");
        editor.AddSet(new SetAsset { Name = "prop", Objects = { new ObjectAsset { Id = "Body" } } });
        more?.Invoke(editor);

        string directory = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "sample.cut-authoring.json");
        File.WriteAllText(path, AuthoringDocumentJson.Write(editor.Document));
        return path;
    }

    [Fact]
    public void 쓰는_법이_틀리면_2이고_안내가_나온다()
    {
        Assert.Equal(2, Run().Code);
        Assert.Contains("mt validate", Run().Out, StringComparison.Ordinal);
        Assert.Equal(0, Run("--help").Code);
        (int code, _, _, string err) = Run("frobnicate", "x");
        Assert.Equal(2, code);
        Assert.Contains("모르는 명령", err, StringComparison.Ordinal);
        Assert.Equal(2, Run("validate").Code);                                  // 문서가 없다
        Assert.Equal(2, Run("pose", WriteDocument(), "--time", "0.1").Code);    // --timeline이 없다
        Assert.Equal(2, Run("pose", WriteDocument(), "--timeline", "nod", "--time", "soon").Code);
        Assert.Equal(2, Run("pose", WriteDocument(), "--timeline").Code);         // 값이 없다

        // 없는 파일·없는 이름은 1 — JSON으로도 남는다.
        (code, JsonNode? json, _, _) = Run("info", "/no/such/file.json");
        Assert.Equal(1, code);
        Assert.False(json!["ok"]!.GetValue<bool>());
        Assert.Equal(1, Run("pose", WriteDocument(), "--timeline", "nope", "--time", "0").Code);
        Assert.Equal(2, Run("cut", WriteDocument(e => e.AddCut("opening")), "opening").Code);          // -o가 없다
        Assert.Equal(1, Run("cut", WriteDocument(), "nope", "-o", Path.Combine(Path.GetTempPath(), "nope.cut.json")).Code);
    }

    [Fact]
    public void info는_문서의_목록을_낸다()
    {
        (int code, JsonNode? json, _, _) = Run("info", WriteDocument(e => e.AddCut("opening")));
        Assert.Equal(0, code);
        Assert.Equal(new[] { "nod", "bow", "opening" }, json!["timelines"]!.AsArray().Select(m => m!["name"]!.GetValue<string>()));
        Assert.Equal(1, json["timelines"]![0]!["events"]!.GetValue<int>());
        Assert.Equal(new[] { "opening" }, json["cuts"]!.AsArray().Select(c => c!["name"]!.GetValue<string>()));
        Assert.Null(json["timelines"]![0]!["end"]); // 종료 처리는 없다
        Assert.Equal(new[] { "actor", "prop" }, json["sets"]!.AsArray().Select(r => r!["name"]!.GetValue<string>()));
        Assert.Null(json["scenes"]);
    }

    [Fact]
    public void validate는_막는_문제가_있으면_1이다()
    {
        (int code, JsonNode? json, _, _) = Run("validate", WriteDocument());
        Assert.Equal(0, code);
        Assert.True(json!["ok"]!.GetValue<bool>());
        Assert.Empty(json["problems"]!.AsArray());

        // 세트에 없는 오브젝트에 놓인 채널 — 막는 문제다.
        (code, json, _, _) = Run("validate", WriteDocument(e => e.AddChannel("nod", "Ghost", ChannelKind.Position)));
        Assert.Equal(1, code);
        Assert.False(json!["ok"]!.GetValue<bool>());
        Assert.Contains(json["problems"]!.AsArray(), p => p!["kind"]!.GetValue<string>() == "timeline" && p["name"]!.GetValue<string>() == "nod" && p["blocking"]!.GetValue<bool>());

        // 알림뿐이면 0이다 — 길이를 줄여 길이 밖에 남은 키.
        (code, json, _, _) = Run("validate", WriteDocument(e => e.SetDuration("nod", 0.3f)));
        Assert.Equal(0, code);
        Assert.False(Assert.Single(json!["problems"]!.AsArray())!["blocking"]!.GetValue<bool>());

        // 재생되지 않는 재생 블록도 알림이다.
        (code, json, _, _) = Run("validate", WriteDocument(e => e.Document.Find("bow")!.Plays.Add(new PlayBlockAsset { Object = "ghost", Timeline = "nod" })));
        Assert.Equal(0, code);
        Assert.NotEmpty(json!["problems"]!.AsArray());
        Assert.All(json["problems"]!.AsArray(), p => Assert.False(p!["blocking"]!.GetValue<bool>()));
        Assert.Contains(json["problems"]!.AsArray(), p => p!["kind"]!.GetValue<string>() == "timeline" && p["name"]!.GetValue<string>() == "bow");
    }

    [Fact]
    public void cut은_컷_파일을_쓰고_막는_문제가_있으면_안_쓴다()
    {
        // 컷 opening(24프레임): 색 사각형 card가 0→12프레임에 x 0→100, 품은 세트 actor의 nod를 6프레임부터 튼다. 이벤트 하나.
        static void Opening(DocumentEditor e)
        {
            e.AddCut("opening", 24);
            e.AddObject("opening", "card");
            e.UpdateObject("opening", "card", new ObjectAsset { Width = 100f, Height = 100f, Color = "#3D5A80", Alpha = true });
            e.AddInstance("opening", "hero", Fixtures.Actor);
            e.AddChannel("opening", "card", ChannelKind.Position);
            e.SetKeys("opening", "card", ChannelKind.Position, Axis.X, new[] { new KeyAsset { T = 0f, V = 0f, Tangent = TangentMode.Linear }, new KeyAsset { T = 0.5f, V = 100f, Tangent = TangentMode.Linear } });
            e.AddPlayBlock("opening", "hero", "nod", 0.25f);
            e.AddEventKey("opening", 0.5f, "sfx", "pop");
        }

        string document = WriteDocument(Opening);
        string target = Path.Combine(Path.GetDirectoryName(document)!, "out", "opening.cut.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        (int code, JsonNode? json, _, _) = Run("cut", document, "opening", "-o", target);
        Assert.Equal(0, code);
        Assert.Equal(("cut/1", "opening", 24), (json!["format"]!.GetValue<string>(), json["cut"]!.GetValue<string>(), json["frames"]!.GetValue<int>()));
        Assert.Equal(Path.GetFullPath(target), json["path"]!.GetValue<string>());

        // 쓴 파일 — 게임이 읽는 모양 그대로 읽힌다. 품은 세트는 오브젝트로 펼쳐지고 재생 블록은 키로 구워진다.
        byte[] bytes = File.ReadAllBytes(target);
        Assert.NotEqual(0xEF, bytes[0]); // BOM이 없다
        CutFileDto file = CutFileJson.Read(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Equal(json["objects"]!.GetValue<int>(), file.objects.Count);
        Assert.Contains(file.objects, o => o.id == "hero.CharacterPortrait_Track_Move_Y");
        CutTrackDto card = file.tracks.Single(t => t.target == "card" && t.what == "x");
        Assert.Equal(new[] { (0f, 0f), (12f, 100f) }, card.keys.Select(k => (k.f, k.v)));
        Assert.Equal(100f / 12f, card.keys[0].o, 3); // 기울기는 프레임당
        CutTrackDto nod = file.tracks.Single(t => t.target == "hero.CharacterPortrait_Track_Move_Y" && t.what == "y");
        Assert.Equal(-18f, CutPlayer.Evaluate(file, 6f + 7.2f, null!).Tree.GetState("hero.CharacterPortrait_Track_Move_Y").AnchoredPosition.Y, 2);
        Assert.Equal(0f, nod.keys[0].v);
        Assert.Equal(("sfx", "pop", 12f), (file.events[0].name, file.events[0].value, file.events[0].f));

        // 앱의 "컷 내보내기"와 같은 글자다(같은 함수).
        AuthoringDocument read = AuthoringDocumentJson.Read(File.ReadAllText(document));
        Assert.Equal(CutFileJson.Write(CutFileJson.Build(read, read.FindCut("opening")!, new Vec2(1920f, 1080f))), File.ReadAllText(target));

        // 막는 문제가 있으면 파일을 안 쓴다.
        string broken = WriteDocument(e =>
        {
            Opening(e);
            e.AddChannel("opening", "Ghost", ChannelKind.Position);
        });
        string brokenOut = Path.Combine(Path.GetDirectoryName(broken)!, "opening.cut.json");
        (code, json, _, _) = Run("cut", broken, "opening", "-o", brokenOut);
        Assert.Equal(1, code);
        Assert.False(json!["ok"]!.GetValue<bool>());
        Assert.NotEmpty(json["problems"]!.AsArray());
        Assert.False(File.Exists(brokenOut));
    }

    [Fact]
    public void pose는_그_시각의_값과_방금_울린_이벤트를_낸다()
    {
        string document = WriteDocument();

        // 타임라인: nod의 0.3초 — 가장 깊은 자리(-18), 이벤트가 방금 울렸다.
        (int code, JsonNode? json, _, _) = Run("pose", document, "--timeline", "nod", "--time", "0.3");
        Assert.Equal(0, code);
        Assert.Equal("actor", json!["set"]!.GetValue<string>());
        Assert.Null(json["mode"]); // 재생 기준은 없다 — 키는 그 프레임의 값이다
        Assert.Equal(-18.0, json["objects"]!["CharacterPortrait_Track_Move_Y"]!["position"]![1]!.GetValue<double>(), 3);
        Assert.Equal("땀", json["events"]![0]!["value"]!.GetValue<string>());

        // 길이를 지나도 마지막 키의 모습이다: bow는 끝에서 z = 8.
        (_, json, _, _) = Run("pose", document, "--timeline", "bow", "--time", "9");
        Assert.Equal(8.0, json!["objects"]!["CharacterPortrait_SwayPivot"]!["euler"]![2]!.GetValue<double>(), 3);

        // 얹힐 세트가 문서에 없는 애니메이션은 1이다.
        Assert.Equal(1, Run("pose", WriteDocument(e => e.AddTimeline("lost", "robot", 1f)), "--timeline", "lost", "--time", "0").Code);
    }

    [Fact]
    public void 손으로_쓴_문서의_탄젠트는_종류대로_세워_읽는다()
    {
        // AI가 쓰는 꼴: 키의 값과 탄젠트 종류만 — 탄젠트 값은 없다.
        string directory = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "hand.cut-authoring.json");
        File.WriteAllText(path, """
            { "schema": "cut-authoring/1",
              "cuts": [ { "name": "slide", "frames": 24, "objects": [ { "id": "card", "width": 100, "height": 100, "color": "#FFFFFF" } ] } ],
              "timelines": [ { "name": "slide", "set": "slide", "duration": 1,
              "channels": [ { "object": "card", "kind": "position",
                "x": { "keys": [ { "t": 0, "v": 0, "tangent": "linear" }, { "t": 1, "v": 100, "tangent": "linear" } ] } } ] } ] }
            """);

        Assert.Equal(0, Run("validate", path).Code);
        (int code, JsonNode? json, _, _) = Run("pose", path, "--timeline", "slide", "--time", "0.25");
        Assert.Equal(0, code);
        Assert.Equal(25.0, json!["objects"]!["card"]!["position"]![0]!.GetValue<double>(), 2); // linear — 탄젠트 0이었다면 15.6쯤이 나온다

        // 내보낸 컷 파일에도 그 탄젠트 값이 실린다(프레임당: 100 ÷ 24).
        string target = Path.Combine(directory, "slide.cut.json");
        Assert.Equal(0, Run("cut", path, "slide", "-o", target).Code);
        CutFileDto file = CutFileJson.Read(File.ReadAllText(target));
        Assert.Equal(100f / 24f, file.tracks.Single().keys[0].o, 3);
    }
}
