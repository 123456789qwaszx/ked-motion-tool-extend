using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

/// <summary>카메라 모션 — 카메라(zoom·pan)를 키와 커브로. 가짜 리그 <c>camera</c> 위의 보통 모션이다.</summary>
public class CameraMotionTests
{
    private static readonly RigSchemaFile RigFile = BuiltinTuning.RigSchemas;

    private static RectNodeTree BuildTree(SceneAsset scene)
        => ScenePreview.Build(RigFile, scene, null, null, BuiltinTuning.Depth, BuiltinTuning.Focus, BuiltinTuning.StageRig);

    /// <summary>
    /// push(1초 commit): zoom 0 → 4, pan x 0 → 100. shake(0.5초 restore): pan y 0 → 20 → 0. nod(캐릭터, 0.6초).
    /// </summary>
    private static MotionEditor NewEditor()
    {
        var editor = new MotionEditor(new MotionDocument());

        editor.AddMotion("push", CameraRig.RigKind, 1f, MotionEnd.Commit, MotionChannelMode.Absolute);
        editor.AddChannel("push", CameraRig.ZoomNode, MotionChannelKind.Position);
        editor.SetKeys("push", CameraRig.ZoomNode, MotionChannelKind.Position, Axis.X, new[] { Linear(0f, 0f), Linear(1f, 4f) });
        editor.AddChannel("push", CameraRig.PanNode, MotionChannelKind.Position);
        editor.SetKeys("push", CameraRig.PanNode, MotionChannelKind.Position, Axis.X, new[] { Linear(0f, 0f), Linear(1f, 100f) });

        editor.AddMotion("shake", CameraRig.RigKind, 0.5f);
        editor.AddChannel("shake", CameraRig.PanNode, MotionChannelKind.Position);
        editor.SetKeys("shake", CameraRig.PanNode, MotionChannelKind.Position, Axis.Y, new[] { Linear(0f, 0f), Linear(0.25f, 20f), Linear(0.5f, 0f) });

        editor.AddMotion("nod", "character", 0.6f);
        editor.AddChannel("nod", "CharacterPortrait_Track_Move_Y", MotionChannelKind.Position);
        editor.SetKeys("nod", "CharacterPortrait_Track_Move_Y", MotionChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.3f, -18f), Fixtures.Key(0.6f, 0f) });
        return editor;
    }

    private static KeyAsset Linear(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Linear };

    private static void Near(float expected, float actual, float tolerance = 1e-3f) => Assert.InRange(actual, expected - tolerance, expected + tolerance);

    [Fact]
    public void 카메라_모션은_보통_모션의_규칙으로_카메라_값을_낸다()
    {
        MotionEditor editor = NewEditor();
        MotionAsset push = editor.Document.Find("push")!;
        var start = new CameraSettings { Zoom = 2f, PanX = 10f, PanY = -5f };

        // absolute: 키 그대로. 안 건드린 성분(pan y)은 시작값.
        CameraSettings half = CameraRig.Pose(start, push, 0.5, MotionChannelMode.Absolute);
        Near(2f, half.Zoom);
        Near(50f, half.PanX);
        Near(-5f, half.PanY);

        // relative: 지금 값 + 키.
        CameraSettings relative = CameraRig.Pose(start, push, 1.0, MotionChannelMode.Relative);
        Near(6f, relative.Zoom);
        Near(110f, relative.PanX);

        // 끝 규칙: commit은 남고 restore는 돌아간다.
        Near(4f, CameraRig.Pose(start, push, 9.0, MotionChannelMode.Absolute).Zoom);
        MotionAsset shake = editor.Document.Find("shake")!;
        Near(15f, CameraRig.Pose(start, shake, 0.25, MotionChannelMode.Relative).PanY);
        Near(-5f, CameraRig.Pose(start, shake, 9.0, MotionChannelMode.Relative).PanY);

        // 코어 규칙으로 검증된다 — 가짜 리그의 노드 표로.
        Assert.Empty(MotionValidator.Validate(editor.Document, CameraRig.WithCamera(RigFile.NodeIds)));
        Assert.Contains(MotionValidator.Validate(editor.Document, RigFile.NodeIds), p => p.Blocking && p.MotionName == "push"); // 표에 없으면 모르는 리그다
        Assert.Null(CameraRig.WithCamera(null));
    }

    [Fact]
    public void 카메라_모션은_motions_json에_나가지_않고_저작본에는_왕복한다()
    {
        MotionEditor editor = NewEditor();
        Assert.True(MotionExport.TryExport(editor.Document, RigFile.NodeIds, out string json, out IReadOnlyList<MotionProblem> problems));
        Assert.Empty(problems);
        Assert.Contains("\"nod\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("push", json, StringComparison.Ordinal);
        Assert.DoesNotContain("camera", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "nod" }, MotionExport.ToClips(editor.Document).Select(c => c.Name));

        string authoring = MotionDocumentJson.Write(editor.Document);
        Assert.Equal(authoring, MotionDocumentJson.Write(MotionDocumentJson.Read(authoring)));

        // 카메라 모션이 규칙을 어겨도 motions.json은 나간다(거기 실리지 않는 모션이다) — 문제 칸은 계속 알린다.
        editor.SetDuration("push", 0.5f);
        Assert.True(MotionExport.TryExport(editor.Document, RigFile.NodeIds, out string again, out _));
        Assert.Equal(json, again);
        Assert.Contains(MotionValidator.Validate(editor.Document, CameraRig.WithCamera(RigFile.NodeIds)), p => p.Blocking && p.MotionName == "push");
    }

    [Fact]
    public void 카메라_모션이_무대를_움직인다()
    {
        MotionEditor editor = NewEditor();
        MotionAsset push = editor.Document.Find("push")!; // absolute(모션의 렌즈)
        MotionAsset nod = editor.Document.Find("nod")!;
        SceneAsset scene = editor.Document.ActiveSceneAsset!;
        RectNodeTree template = BuildTree(scene);

        // 뷰포트가 카메라 모션을 보이는 길 — 씬의 카메라 값에 모션을 얹어 무대의 카메라 노드를 다시 세운다.
        RectNodeTree PoseAt(double t)
        {
            RectNodeTree tree = template.Clone();
            StageShotResponse.Apply(tree, CameraRig.Pose(scene.Camera, push, t, push.PreviewMode));
            return tree;
        }

        string panKey = StageNodes.KeyOf(StageNodes.PanRoot);
        string zoomKey = StageNodes.KeyOf(StageNodes.ZoomRoot);

        Near(0f, PoseAt(0.0).GetState(panKey).AnchoredPosition.X);
        Near(50f, PoseAt(0.5).GetState(panKey).AnchoredPosition.X);
        Near(ShotIntentMath.EvaluateCameraScale(2f), PoseAt(0.5).GetState(zoomKey).LocalScale.X);

        // 캐릭터 모션은 카메라와 따로 — 제 노드만 움직인다.
        RectNodeTree nodding = ScenePreview.Pose(template, scene.Find("s1")!, nod, 0.3);
        Near(-18f, nodding.GetState("s1/CharacterPortrait_Track_Move_Y").AnchoredPosition.Y);
        Near(0f, nodding.GetState(panKey).AnchoredPosition.X);

        // 끝난 뒤의 무대 = 카메라를 그 값으로 둔 씬(층 패럴랙스까지).
        var expectedScene = new SceneAsset { Name = scene.Name, Objects = scene.Objects, Camera = new CameraSettings { Zoom = 4f, PanX = 100f, PanY = scene.Camera.PanY } };
        RectNodeTree expected = BuildTree(expectedScene);
        RectNodeTree after = PoseAt(5.0);

        foreach (string key in expected.Keys)
        {
            RectNodeState e = expected.GetState(key);
            RectNodeState a = after.GetState(key);
            Assert.True(
                Math.Abs(e.AnchoredPosition.X - a.AnchoredPosition.X) < 1e-3f && Math.Abs(e.AnchoredPosition.Y - a.AnchoredPosition.Y) < 1e-3f && Math.Abs(e.LocalScale.X - a.LocalScale.X) < 1e-5f,
                $"노드 {key}가 다르다");
        }
    }
}
