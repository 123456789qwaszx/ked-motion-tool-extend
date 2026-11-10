using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;
using Xunit;

namespace Mt.Authoring.Tests;

/// <summary>컷(<c>PLAN-standalone.md</c> §6) — 오브젝트 계층 + 카메라 + 타임라인 하나.</summary>
public class CutTests
{
    [Fact]
    public void 컷을_만들면_빈_오브젝트_계층과_그_길이의_타임라인이_함께_선다()
    {
        var editor = new MotionEditor(new MotionDocument());
        long revision = editor.Revision;

        CutAsset cut = editor.AddCut("arrival");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(("arrival", 240, 10f), (cut.Name, cut.Frames, cut.Seconds));
        Assert.Empty(cut.Nodes);

        // 타임라인 — 같은 이름의 애니메이션 하나. 키는 그 프레임의 모습이다(절대값, 끝의 모습이 남는다).
        MotionAsset timeline = editor.Document.Find("arrival")!;
        Assert.Equal(("arrival", 10f, MotionEnd.Commit, MotionChannelMode.Absolute), (timeline.Rig, timeline.Duration, timeline.End, timeline.PreviewMode));

        // 오브젝트 계층은 컷의 이름으로 찾는다 — 세트의 목록에는 없다.
        Assert.Same(cut.Rig, editor.Document.FindRig("arrival"));
        Assert.Empty(editor.Document.Rigs);

        // 되돌리면 통째로 사라진다.
        editor.Undo();
        Assert.Empty(editor.Document.Cuts);
        Assert.Null(editor.Document.Find("arrival"));
    }

    [Fact]
    public void 컷에_오브젝트를_놓고_옮기는_것은_세트의_명령_그대로다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddCut("arrival");

        // 오브젝트를 바로 놓는다 — 세트를 먼저 만들지 않는다. 계층 · 자리 · 색이 세트와 같다.
        editor.AddRigNode("arrival", "sky");
        editor.UpdateRigNode("arrival", "sky", new RigNodeAsset { Width = 1920f, Height = 1080f, Color = "#243B6B", Alpha = true });
        editor.AddRigNode("arrival", "hero", null, image: true);
        editor.AddRigNode("arrival", "arm", "hero", image: true);
        CutAsset cut = editor.Document.FindCut("arrival")!;
        Assert.Equal(new[] { ("sky", ""), ("hero", ""), ("arm", "hero") }, cut.Nodes.Select(n => (n.Id, n.Parent)));

        // 키를 타임라인에 놓는다.
        editor.AddChannel("arrival", "hero", MotionChannelKind.Position);
        editor.SetKeys("arrival", "hero", MotionChannelKind.Position, Axis.X, new[] { new KeyAsset { T = 0f, V = -400f }, new KeyAsset { T = 10f, V = 0f } });
        Assert.Single(editor.Document.Find("arrival")!.Channels);

        // 세트를 품는다. 컷은 품을 수 없다.
        editor.AddRig(new RigAsset { Kind = "prop", Nodes = { new RigNodeAsset { Id = "Body", Alpha = true } } });
        editor.AddRigInstance("arrival", "prop", "prop");
        Assert.Equal("prop", cut.Rig.Find("prop")!.Set);
        editor.AddCut("other");
        Assert.Throws<InvalidOperationException>(() => editor.AddRigInstance("other", "inner", "arrival"));
        Assert.Throws<InvalidOperationException>(() => editor.AddRigInstance("prop", "inner", "arrival"));

        // 품긴 세트는 못 지운다(컷이 품고 있다). 컷은 세트의 명령으로 지우거나 이름을 바꾸지 못한다.
        Assert.Throws<InvalidOperationException>(() => editor.RemoveRig("prop"));
        Assert.Throws<InvalidOperationException>(() => editor.RemoveRig("arrival"));
        Assert.Throws<InvalidOperationException>(() => editor.RenameRig("arrival", "x"));

        // 컷에서 고른 것을 새 세트로 뺀다 — 그 자리에 인스턴스가 선다(묶기).
        Assert.Equal("actor", editor.ExtractRigNodes("arrival", new[] { "hero" }, "actor"));
        Assert.Equal(new[] { "sky", "actor", "prop" }, cut.Nodes.Select(n => n.Id));
        Assert.Equal(new[] { "hero", "arm" }, editor.Document.FindRig("actor")!.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void 맨_위_오브젝트는_깊이를_가지고_밑의_것은_부모를_따른다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddCut("arrival");
        editor.AddRigNode("arrival", "hero", null, image: true);
        editor.AddRigNode("arrival", "arm", "hero", image: true);
        RigNodeAsset hero = editor.Document.FindCut("arrival")!.Rig.Find("hero")!;
        Assert.Null(hero.Depth); // 안 주면 mid

        long revision = editor.Revision;
        editor.SetCutNodeDepth("arrival", "hero", 16f);
        Assert.Equal((16f, revision + 1), (hero.Depth, editor.Revision));
        editor.SetCutNodeDepth("arrival", "hero", 16f); // 같은 값 — 무기록
        Assert.Equal(revision + 1, editor.Revision);

        Assert.Throws<InvalidOperationException>(() => editor.SetCutNodeDepth("arrival", "arm", 10f));   // 밑의 것
        Assert.Throws<InvalidOperationException>(() => editor.SetCutNodeDepth("arrival", "hero", -1f));  // 범위 밖
        Assert.Throws<InvalidOperationException>(() => editor.SetCutNodeDepth("arrival", "hero", float.NaN));
        Assert.Throws<KeyNotFoundException>(() => editor.SetCutNodeDepth("nope", "hero", 10f));

        // 자리를 고쳐도 깊이는 그대로다.
        editor.UpdateRigNode("arrival", "hero", new RigNodeAsset { X = 50f, Width = 100f, Height = 100f, Image = true, Alpha = true });
        Assert.Equal(16f, hero.Depth);
        editor.SetCutNodeDepth("arrival", "hero", null);
        Assert.Null(hero.Depth);
    }

    [Fact]
    public void 컷의_이름_길이_카메라를_바꾸면_타임라인이_따라오고_지우면_함께_사라진다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddCut("arrival");
        editor.AddRigNode("arrival", "hero", null, image: true);
        editor.AddSkin("arrival", "arrival");
        editor.SetSkinPart("arrival", "hero", "art/hero.png");

        // 길이 — 프레임. 타임라인의 길이가 따라온다(한 칸).
        long revision = editor.Revision;
        editor.SetCutFrames("arrival", 120);
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal((120, 5f), (editor.Document.FindCut("arrival")!.Frames, editor.Document.Find("arrival")!.Duration));
        Assert.Throws<InvalidOperationException>(() => editor.SetCutFrames("arrival", 0));

        // 카메라.
        editor.SetCutCamera("arrival", new CameraSettings { Zoom = 2f, PanX = 30f });
        Assert.Equal((2f, 30f, 0f), (editor.Document.FindCut("arrival")!.Camera.Zoom, editor.Document.FindCut("arrival")!.Camera.PanX, editor.Document.FindCut("arrival")!.Camera.PanY));

        // 이름 — 타임라인과 스킨이 따라온다(한 칸).
        revision = editor.Revision;
        editor.RenameCut("arrival", "station");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Null(editor.Document.FindCut("arrival"));
        Assert.Equal("station", editor.Document.Find("station")!.Rig);
        Assert.Equal("station", editor.Document.FindSkin("station")!.Rig);
        Assert.Same(editor.Document.FindCut("station")!.Rig, editor.Document.FindRig("station"));

        // 막는 이름: 대문자 · 있는 세트/컷/애니메이션.
        editor.AddRig(new RigAsset { Kind = "prop" });
        Assert.Throws<InvalidOperationException>(() => editor.AddCut("Station"));
        Assert.Throws<InvalidOperationException>(() => editor.AddCut("station"));
        Assert.Throws<InvalidOperationException>(() => editor.AddCut("prop"));
        Assert.Throws<InvalidOperationException>(() => editor.RenameCut("station", "prop"));

        // 저작본 왕복 — 컷은 cuts로, 오브젝트는 objects로 적힌다. 세트의 목록에는 안 섞인다.
        string json = MotionDocumentJson.Write(editor.Document);
        Assert.Contains("\"cuts\"", json, StringComparison.Ordinal);
        Assert.Contains("\"objects\"", json, StringComparison.Ordinal);
        MotionDocument read = MotionDocumentJson.Read(json);
        Assert.Equal(("station", 120), (read.Cuts[0].Name, read.Cuts[0].Frames));
        Assert.Equal("hero", read.FindRig("station")!.Nodes[0].Id);
        Assert.Equal(json, MotionDocumentJson.Write(read));

        // 지우면 타임라인과 스킨도 함께(한 칸). 되돌리면 돌아온다.
        revision = editor.Revision;
        editor.RemoveCut("station");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Null(editor.Document.Find("station"));
        Assert.Null(editor.Document.FindSkin("station"));
        editor.Undo();
        Assert.NotNull(editor.Document.Find("station"));
        Assert.Equal("hero", editor.Document.FindRig("station")!.Nodes[0].Id);
    }

    // ── 무대에 세운다 — 깊이와 카메라 ─────────────────────────────────

    private static readonly RigSchemaFile Dump = RigSchemaFile.Load(Fixtures.PathOf("rig-schemas.json"));

    /// <summary>sky(깊이 0 · far) · hero(깊이 없음 = mid) · near(깊이 20 · close), 전부 무대 가운데의 100×100. hero 밑에 arm(+50).</summary>
    private static (MotionEditor Editor, CutAsset Cut) StageCut()
    {
        var editor = new MotionEditor(new MotionDocument());
        CutAsset cut = editor.AddCut("arrival");

        foreach (string id in new[] { "sky", "hero", "near" })
        {
            editor.AddRigNode("arrival", id, null, image: true);
            editor.UpdateRigNode("arrival", id, new RigNodeAsset { Width = 100f, Height = 100f, Image = true, Alpha = true });
        }

        editor.AddRigNode("arrival", "arm", "hero", image: true);
        editor.UpdateRigNode("arrival", "arm", new RigNodeAsset { X = 50f, Width = 20f, Height = 20f, Image = true, Alpha = true });
        editor.SetCutNodeDepth("arrival", "sky", 0f);
        editor.SetCutNodeDepth("arrival", "near", 20f);
        return (editor, cut);
    }

    private static RectNodeTree Staged(MotionEditor editor, CutAsset cut, CameraSettings camera, DepthTuningFile? depth)
    {
        editor.SetCutCamera(cut.Name, camera);
        RectNodeTree tree = CutStage.Build(RigAssets.Compose(Dump, editor.Document), editor.Document.FindCut(cut.Name)!, "cut/")!;
        CutStage.Apply(tree, editor.Document.FindCut(cut.Name)!, "cut/", depth);
        return tree;
    }

    [Fact]
    public void 카메라를_밀면_먼_것은_덜_가까운_것은_더_따라오고_당기면_가까운_것이_더_커진다()
    {
        (MotionEditor editor, CutAsset cut) = StageCut();
        RectNodeTree rest = Staged(editor, cut, new CameraSettings(), null);
        float X(RectNodeTree tree, string id) => tree.TransformPoint("cut/" + id, Vec3.Zero).X;
        float Width(RectNodeTree tree, string id) => tree.TransformPoint("cut/" + id, new Vec3(50f, 0f, 0f)).X - tree.TransformPoint("cut/" + id, new Vec3(-50f, 0f, 0f)).X;

        // 카메라가 가만히 있고 깊이 커브가 없으면 지은 그대로 선다.
        Assert.Equal((0f, 0f, 0f), (X(rest, "sky"), X(rest, "hero"), X(rest, "near")));
        Assert.Equal(100f, Width(rest, "hero"), 3);
        Assert.Equal(50f, X(rest, "arm"), 3); // 밑의 것은 부모를 따른다

        // 밀기 100 — mid는 그대로 100, far는 덜(100 × (1 − 0.04)), close는 더(100 × (1 + 0.2)). 런타임의 층별 상수 그대로.
        RectNodeTree panned = Staged(editor, cut, new CameraSettings { PanX = 100f }, null);
        Assert.Equal(96f, X(panned, "sky"), 3);
        Assert.Equal(100f, X(panned, "hero"), 3);
        Assert.Equal(120f, X(panned, "near"), 3);
        Assert.Equal(150f, X(panned, "arm"), 3);

        // 당기기 4(배율 1.2) — mid는 1.2배, far는 덜(1.2^(1 − 0.15)), close는 더(1.2^(1 + 0.16)).
        RectNodeTree zoomed = Staged(editor, cut, new CameraSettings { Zoom = 4f }, null);
        Assert.Equal(120f, Width(zoomed, "hero"), 2);
        Assert.Equal(100f * MathF.Pow(1.2f, 0.85f), Width(zoomed, "sky"), 2);
        Assert.Equal(100f * MathF.Pow(1.2f, 1.16f), Width(zoomed, "near"), 2);

        // 깊이가 눈금 사이(12 = back 10과 mid 14의 가운데)면 따르는 정도도 그 사이다 — 끊기지 않는다.
        (Vec2 pan, float zoom) = CutStage.ResponseAt(12f);
        Assert.Equal((-0.01f, -0.04f), (MathF.Round(pan.X, 4), MathF.Round(zoom, 4)));
        Assert.Equal(CutStage.ResponseAt(0f), CutStage.ResponseAt(-5f));  // far보다 멀어도 더 달라지지 않는다
        Assert.Equal(CutStage.ResponseAt(20f), CutStage.ResponseAt(40f));
    }

    [Fact]
    public void 깊이에_따라_크기가_달라지고_자리는_그대로이며_먼_것부터_그린다()
    {
        (MotionEditor editor, CutAsset cut) = StageCut();
        var depth = DepthTuningFile.Load(Fixtures.PathOf("presets/depth.json"));
        editor.UpdateRigNode("arrival", "sky", new RigNodeAsset { X = 300f, Width = 100f, Height = 100f, Image = true, Alpha = true });
        RectNodeTree tree = Staged(editor, cut, new CameraSettings(), depth);
        float Width(string id) => tree.TransformPoint("cut/" + id, new Vec3(50f, 0f, 0f)).X - tree.TransformPoint("cut/" + id, new Vec3(-50f, 0f, 0f)).X;

        // mid는 지은 그대로, 먼 것은 작게, 가까운 것은 크게 — 런타임의 깊이 커브.
        Assert.Equal(100f, Width("hero"), 3);
        Assert.True(Width("sky") < 99f, $"sky {Width("sky")}");
        Assert.True(Width("near") > 101f, $"near {Width("near")}");
        Assert.Equal(1f, CutStage.SizeAt(14f, depth), 4);

        // 제 원점을 중심으로 줄어든다 — 자리는 그대로다.
        Assert.Equal(300f, tree.TransformPoint("cut/sky", Vec3.Zero).X, 3);

        // 그리는 순서 — 먼 것부터. 밑의 것은 제 맨 위 오브젝트를 따라간다. 깊이를 바꾸면 차례가 바뀐다.
        RigAssets.Compose(Dump, editor.Document).TryGetRig("arrival", out RigSchemaRigDto dto);
        string[] Order() => CutStage.DrawOrder(cut, dto.nodes).Select(n => n.id).Where(id => id != RigSchemaLoader.RootKey && !CutStage.IsCameraNode(id)).ToArray();
        Assert.Equal(new[] { "sky", "hero", "arm", "near" }, Order());
        editor.SetCutNodeDepth("arrival", "hero", 30f);
        Assert.Equal(new[] { "sky", "near", "hero", "arm" }, Order());
    }

    // ── 타임라인 — 카메라가 같은 타임라인의 트랙이다 ───────────────────

    [Fact]
    public void 카메라의_키는_컷의_타임라인에_놓이고_그_시각의_카메라가_오브젝트를_움직인다()
    {
        (MotionEditor editor, CutAsset cut) = StageCut();
        editor.SetCutCamera("arrival", new CameraSettings { PanX = 10f });
        Assert.False(editor.CutCameraIsKeyed("arrival"));

        // 2초에 밀기 0, 6초에 밀기 200 — 각각 한 칸. 키가 처음(0초)과 끝(10초)에 없어도 된다.
        long revision = editor.Revision;
        editor.SetCutCameraKey("arrival", 2f, new CameraSettings());
        editor.SetCutCameraKey("arrival", 6f, new CameraSettings { PanX = 200f });
        Assert.Equal(revision + 2, editor.Revision);
        Assert.True(editor.CutCameraIsKeyed("arrival"));
        RigSchemaFile rig = RigAssets.Compose(Dump, editor.Document);
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, null, rig.AlphaNodeIds));

        // 그 시각의 포즈 — 타임라인을 얹은 뒤 카메라와 깊이를 넣는다. 키 앞뒤로는 그 키의 값이 이어진다.
        float HeroX(double t)
        {
            RectNodeTree posed = MotionPreview.Pose(CutStage.Build(rig, editor.Document.FindCut("arrival")!, "cut/")!, editor.Document.Find("arrival")!, t, MotionChannelMode.Absolute, null, "cut/");
            CutStage.Apply(posed, editor.Document.FindCut("arrival")!, "cut/", null);
            return posed.TransformPoint("cut/hero", Vec3.Zero).X;
        }

        Assert.Equal(0f, HeroX(0), 1);     // 첫 키 앞 — 첫 키의 값(가만히 선 값 10이 아니다: 키가 있는 트랙은 키가 정한다)
        Assert.Equal(0f, HeroX(2), 1);
        Assert.InRange(HeroX(4), 60f, 140f);
        Assert.Equal(200f, HeroX(6), 1);
        Assert.Equal(200f, HeroX(9), 1);   // 마지막 키 뒤 — 그 값이 이어진다

        // 같은 시각에 다시 놓으면 그 키를 대신한다. 길이 밖은 막힌다.
        editor.SetCutCameraKey("arrival", 6f, new CameraSettings { PanX = 100f, Zoom = 4f });
        Assert.Equal(2, editor.Document.Find("arrival")!.FindChannel(CutStage.CameraPan, MotionChannelKind.Position)!.X!.Keys.Count);
        Assert.Equal(120f, HeroX(6), 1); // 밀기 100 × 당기기 4(배율 1.2)
        Assert.Throws<InvalidOperationException>(() => editor.SetCutCameraKey("arrival", 11f, new CameraSettings()));

        // 오브젝트의 키도 하나뿐이어도, 처음과 끝에 없어도 된다 — 컷의 타임라인만 그렇다(세트의 애니메이션은 옛 규칙 그대로).
        editor.AddChannel("arrival", "hero", MotionChannelKind.Position);
        editor.AddKey("arrival", "hero", MotionChannelKind.Position, Axis.Y, new KeyAsset { T = 3f, V = 50f });
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, null, rig.AlphaNodeIds));
        editor.AddRig(new RigAsset { Kind = "prop", Nodes = { new RigNodeAsset { Id = "Body", Alpha = true } } });
        editor.AddMotion("prop_in", "prop", 1f, MotionEnd.Commit);
        editor.AddChannel("prop_in", "Body", MotionChannelKind.Position);
        editor.AddKey("prop_in", "Body", MotionChannelKind.Position, Axis.Y, new KeyAsset { T = 0.5f, V = 50f });
        Assert.NotEmpty(MotionValidator.Validate(editor.Document, RigAssets.Compose(Dump, editor.Document).NodeIds));
    }
}
