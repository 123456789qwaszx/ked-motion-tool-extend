using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Sets;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

/// <summary>불투명도 채널(<c>PLAN-parts.md</c> P4) — 알파를 키와 커브로.</summary>
public class AlphaChannelTests
{
    private const string Part = "Badge_Part1";
    private const string Body = "Badge_Body";
    private const string SpriteRoot = "CharacterPortraitSprite_Root";

    /// <summary>조각 세트(badge)와 세트 actor를 세운 한 벌.</summary>
    private static readonly BuiltSets BuiltSets = Fixtures.SetOf(new AuthoringDocument { Sets = { PartsSetFixture.Set(), Fixtures.ActorSet() } });

    private static KeyAsset Linear(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Linear };

    /// <summary>조각 세트와 그 애니메이션 둘. fade(1초): Part1 알파 1 → 0. dim(1초): Body 알파 1 → 0.5.</summary>
    private static DocumentEditor NewEditor()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(PartsSetFixture.Set());
        editor.AddTimeline("fade", PartsSetFixture.SetName, 1f);
        editor.AddChannel("fade", Part, ChannelKind.Alpha);
        editor.SetKeys("fade", Part, ChannelKind.Alpha, Axis.X, new[] { Linear(0f, 1f), Linear(1f, 0f) });

        editor.AddTimeline("dim", PartsSetFixture.SetName, 1f);
        editor.AddChannel("dim", Body, ChannelKind.Alpha);
        editor.SetKeys("dim", Body, ChannelKind.Alpha, Axis.X, new[] { Linear(0f, 1f), Linear(1f, 0.5f) });
        return editor;
    }

    /// <summary>조각 리그를 <c>e1/</c> 접두사로 세운 트리.</summary>
    private static RectNodeTree Template() => BuiltSets.BuildTree(PartsSetFixture.SetName, "e1/");

    private static void Near(float expected, float actual) => Assert.InRange(actual, expected - 1e-3f, expected + 1e-3f);

    [Fact]
    public void 알파_채널은_트리가_아니라_불투명도_표에_얹힌다()
    {
        DocumentEditor editor = NewEditor();
        RectNodeTree template = Template();
        TimelineAsset fade = editor.Document.Find("fade")!;
        var alphas = new Dictionary<string, float>();

        RectNodeTree pose = TimelinePose.Pose(template, fade, 0.25, null, "e1/", alphas);
        Near(0.75f, alphas["e1/" + Part]);
        Assert.Equal(template.GetState("e1/" + Part).AnchoredPosition, pose.GetState("e1/" + Part).AnchoredPosition); // 트리는 그대로

        // 키 값이 곧 불투명도다 — 표에 이미 있던 값에 곱하지 않고 그 값으로 적는다.
        alphas = new Dictionary<string, float> { ["e1/" + Part] = 0.5f };
        TimelinePose.Pose(template, fade, 0.25, null, "e1/", alphas);
        Near(0.75f, alphas["e1/" + Part]);

        // 끝: 길이를 지나도 마지막 키의 값(0)이 남는다.
        alphas = new Dictionary<string, float>();
        TimelinePose.Pose(template, fade, 9.0, null, "e1/", alphas);
        Near(0f, alphas["e1/" + Part]);

        // 알파 채널이 없는 오브젝트는 표에 없다(1로 친다).
        Assert.False(alphas.ContainsKey("e1/" + Body));

        // 표를 안 주면 알파 채널은 건너뛴다(던지지 않는다).
        TimelinePose.Pose(template, fade, 0.5, null, "e1/");
    }

    [Fact]
    public void 알파_채널은_저작본을_왕복하고_컷_파일의_alpha_트랙으로_나간다()
    {
        DocumentEditor editor = NewEditor();

        // 저작본 왕복.
        string authoring = AuthoringDocumentJson.Write(editor.Document);
        Assert.Contains("\"kind\": \"alpha\"", authoring, StringComparison.Ordinal);
        Assert.Equal(authoring, AuthoringDocumentJson.Write(AuthoringDocumentJson.Read(authoring)));

        // 컷 파일 — 컷의 오브젝트에 놓은 알파 키가 alpha 트랙으로(시각은 프레임).
        editor.AddCut("shot", 24);
        editor.AddObject("shot", "card");
        editor.UpdateObject("shot", "card", new ObjectAsset { Width = 100f, Height = 100f, Color = "#FFFFFF", Alpha = true });
        editor.AddChannel("shot", "card", ChannelKind.Alpha);
        editor.SetKeys("shot", "card", ChannelKind.Alpha, Axis.X, new[] { Linear(0f, 1f), Linear(0.5f, 0.25f) });
        CutFileDto file = CutFileJson.Read(CutFileJson.Write(CutFileJson.Build(editor.Document, editor.Document.FindCut("shot")!, new Vec2(1920f, 1080f))));
        CutTrackDto track = Assert.Single(file.tracks);
        Assert.Equal(("card", "alpha"), (track.target, track.what));
        Assert.Equal(new[] { (0f, 1f), (12f, 0.25f) }, track.keys.Select(k => (k.f, k.v)));
        Near(0.625f, CutPlayer.Evaluate(file, 6f, null!).Alpha["card"]);
    }

    [Fact]
    public void 규칙은_성분_하나와_0에서_1과_받을_수_있는_노드를_본다()
    {
        DocumentEditor editor = NewEditor();
        Assert.Empty(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds, BuiltSets.AlphaNodeIds));

        // Y 성분은 없다 · 키 값은 0~1.
        editor.SetKeys("fade", Part, ChannelKind.Alpha, Axis.Y, new[] { Linear(0f, 1f), Linear(1f, 1f) });
        Assert.Contains(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds), p => p.Blocking && p.Message.Contains("X 성분만", StringComparison.Ordinal));
        editor.Undo();
        editor.SetKeys("fade", Part, ChannelKind.Alpha, Axis.X, new[] { Linear(0f, 1.5f), Linear(1f, 0f) });
        Assert.Contains(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds), p => p.Blocking && p.Message.Contains("0~1", StringComparison.Ordinal));
        editor.Undo();

        // "불투명도 받기"가 꺼진 오브젝트에는 못 놓는다 — 내보내기를 막는 문제다.
        editor.AddChannel("fade", "Badge_Anchor", ChannelKind.Alpha);
        editor.SetKeys("fade", "Badge_Anchor", ChannelKind.Alpha, Axis.X, new[] { Linear(0f, 1f), Linear(1f, 0f) });
        ValidationProblem problem = Assert.Single(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds, BuiltSets.AlphaNodeIds));
        Assert.True(problem.Blocking);
        Assert.Contains("불투명도를 받을 수 없는 노드", problem.Message, StringComparison.Ordinal);

        Assert.Equal(new[] { "Badge_Body", "Badge_Part1", "Badge_Part2", "Badge_Part3", "Badge_Part4" }, BuiltSets.AlphaNodeIds[PartsSetFixture.SetName].OrderBy(n => n, StringComparer.Ordinal));
        Assert.Contains(SpriteRoot, BuiltSets.AlphaNodeIds[Fixtures.Actor]);
    }
}
