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

        // 타임라인 — 같은 이름의 애니메이션 하나. 키는 그 프레임의 모습이다.
        MotionAsset timeline = editor.Document.Find("arrival")!;
        Assert.Equal(("arrival", 10f), (timeline.Rig, timeline.Duration));

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
        RectNodeTree tree = CutStage.Build(Fixtures.RigOf(editor.Document), editor.Document.FindCut(cut.Name)!, "cut/")!;
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
        Fixtures.RigOf(editor.Document).TryGetRig("arrival", out RigSchemaRigDto dto);
        string[] Order() => CutStage.DrawOrder(cut, dto.nodes).Select(n => n.id).Where(id => id != RigSchemaLoader.RootKey && !CutStage.IsPointNode(id)).ToArray();
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
        RigSchemaFile rig = Fixtures.RigOf(editor.Document);
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, rig.AlphaNodeIds));

        // 그 시각의 포즈 — 타임라인을 얹은 뒤 카메라와 깊이를 넣는다. 키 앞뒤로는 그 키의 값이 이어진다.
        float HeroX(double t)
        {
            RectNodeTree posed = MotionPreview.Pose(CutStage.Build(rig, editor.Document.FindCut("arrival")!, "cut/")!, editor.Document.Find("arrival")!, t, null, "cut/");
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

        // 오브젝트의 키도 하나뿐이어도, 처음과 끝에 없어도 된다 — 컷의 타임라인도 묶음(세트)의 애니메이션도 같다.
        editor.AddChannel("arrival", "hero", MotionChannelKind.Position);
        editor.AddKey("arrival", "hero", MotionChannelKind.Position, Axis.Y, new KeyAsset { T = 3f, V = 50f });
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, rig.AlphaNodeIds));
        editor.AddRig(new RigAsset { Kind = "prop", Nodes = { new RigNodeAsset { Id = "Body", Alpha = true } } });
        editor.AddMotion("prop_in", "prop", 1f);
        editor.AddChannel("prop_in", "Body", MotionChannelKind.Position);
        editor.AddKey("prop_in", "Body", MotionChannelKind.Position, Axis.Y, new KeyAsset { T = 0.5f, V = 50f });
        Assert.Empty(MotionValidator.Validate(editor.Document, Fixtures.RigOf(editor.Document).NodeIds));
    }

    [Fact]
    public void 컷에서_키가_처음과_끝에_없는_오브젝트를_새_세트로_빼도_문제가_없고_재생이_그대로다()
    {
        (MotionEditor editor, CutAsset _) = StageCut();
        KeyAsset K(float t, float v) => new() { T = t, V = v };
        editor.AddChannel("arrival", "hero", MotionChannelKind.Position);
        editor.SetKeys("arrival", "hero", MotionChannelKind.Position, Axis.X, new[] { K(2f, -200f), K(6f, 300f) }); // 0초에도 10초에도 키가 없다
        editor.AddKey("arrival", "hero", MotionChannelKind.Position, Axis.Y, K(4f, 80f));                            // 하나뿐인 키
        editor.AddChannel("arrival", "arm", MotionChannelKind.Euler);
        editor.SetKeys("arrival", "arm", MotionChannelKind.Euler, Axis.Z, new[] { K(1f, 0f), K(3f, 90f) });

        Vec2 At(string node, double t)
        {
            RigSchemaFile rig = Fixtures.RigOf(editor.Document);
            CutAsset cut = editor.Document.FindCut("arrival")!;
            RectNodeTree posed = MotionPreview.Pose(CutStage.Build(rig, cut, "cut/")!, editor.Document.Find("arrival")!, t, null, "cut/");
            CutStage.Apply(posed, cut, "cut/", null);
            return posed.TransformPoint("cut/" + node, new Vec3(10f, 0f, 0f)).XY;
        }

        double[] times = { 0, 1, 2, 3.5, 4, 6, 8, 10 };
        var before = times.Select(t => (At("hero", t), At("arm", t))).ToList();

        // 빼기 — 키들이 그대로 세트의 애니메이션이 되고, 컷은 그것을 재생 블록으로 튼다. 막는 문제가 없다.
        editor.ExtractRigNodes("arrival", new[] { "hero" }, "cast");
        RigSchemaFile composed = Fixtures.RigOf(editor.Document);
        Assert.Empty(MotionValidator.Validate(editor.Document, composed.NodeIds, composed.AlphaNodeIds));
        Assert.Equal(new[] { 2f, 6f }, editor.Document.Find("cast_arrival")!.FindChannel("hero", MotionChannelKind.Position)!.X!.Keys.Select(k => k.T));

        // 재생은 그대로다 — 첫 키 앞과 마지막 키 뒤로 그 키의 값이 이어지는 것까지.
        for (int i = 0; i < times.Length; i++)
        {
            Vec2 hero = At("cast.hero", times[i]);
            Vec2 arm = At("cast.arm", times[i]);
            Assert.True(MathF.Abs(before[i].Item1.X - hero.X) < 1.5f && MathF.Abs(before[i].Item1.Y - hero.Y) < 1.5f, $"hero @ {times[i]}: {before[i].Item1.X},{before[i].Item1.Y} → {hero.X},{hero.Y}");
            Assert.True(MathF.Abs(before[i].Item2.X - arm.X) < 1.5f && MathF.Abs(before[i].Item2.Y - arm.Y) < 1.5f, $"arm @ {times[i]}: {before[i].Item2.X},{before[i].Item2.Y} → {arm.X},{arm.Y}");
        }
    }

    // ── 그림 키 — 계단 키(표정 바꾸기) ────────────────────────────────

    [Fact]
    public void 그림_키는_그_프레임부터_그림을_바꾸고_다음_키까지_이어진다()
    {
        (MotionEditor editor, CutAsset _) = StageCut();
        MotionAsset Timeline() => editor.Document.Find("arrival")!;

        // 2초에 smile, 5초에 calm — 각각 한 칸. 시각 순으로 선다.
        long revision = editor.Revision;
        Assert.Equal(0, editor.AddImageKey("arrival", "hero", 5f, "art/hero_calm.png"));
        Assert.Equal(0, editor.AddImageKey("arrival", "hero", 2f, "art/hero_smile.png"));
        Assert.Equal(revision + 2, editor.Revision);
        Assert.Equal(new[] { (2f, "art/hero_smile.png"), (5f, "art/hero_calm.png") }, Timeline().Swaps.Select(s => (s.T, s.Image)));

        // 계단 — 첫 키 앞은 원래 그림(null = 스킨의 것), 키부터 다음 키 직전까지 그 그림. 사이값이 없다.
        Assert.Null(MotionSwaps.ImageAt(Timeline(), "hero", 1.99));
        Assert.Equal("art/hero_smile.png", MotionSwaps.ImageAt(Timeline(), "hero", 2.0));
        Assert.Equal("art/hero_smile.png", MotionSwaps.ImageAt(Timeline(), "hero", 4.99));
        Assert.Equal("art/hero_calm.png", MotionSwaps.ImageAt(Timeline(), "hero", 9.0));
        Assert.Null(MotionSwaps.ImageAt(Timeline(), "sky", 9.0)); // 다른 오브젝트는 그대로

        // 같은 오브젝트의 같은 시각에 다시 놓으면 그 키를 대신한다.
        editor.AddImageKey("arrival", "hero", 2f, "art/hero_angry.png");
        Assert.Equal(2, Timeline().Swaps.Count);
        Assert.Equal("art/hero_angry.png", MotionSwaps.ImageAt(Timeline(), "hero", 3.0));

        // 저작본 왕복 · 검증.
        string json = MotionDocumentJson.Write(editor.Document);
        Assert.Contains("\"swaps\"", json, StringComparison.Ordinal);
        Assert.Equal(json, MotionDocumentJson.Write(MotionDocumentJson.Read(json)));
        RigSchemaFile rig = Fixtures.RigOf(editor.Document);
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, rig.AlphaNodeIds));

        // 막는 것: 그림 없는 키 · 길이 밖 · 그림을 그리지 않는 오브젝트.
        revision = editor.Revision;
        editor.AddRigNode("arrival", "group");
        revision = editor.Revision;
        Assert.Throws<InvalidOperationException>(() => editor.AddImageKey("arrival", "hero", 1f, " "));
        Assert.Throws<InvalidOperationException>(() => editor.AddImageKey("arrival", "hero", 11f, "art/x.png"));
        Assert.Throws<InvalidOperationException>(() => editor.AddImageKey("arrival", "group", 1f, "art/x.png"));
        Assert.Equal(revision, editor.Revision);

        // 오브젝트의 이름을 바꾸면 그림 키가 따라오고, 새 세트로 빼면 그 세트의 애니메이션으로 가서 재생 블록으로 돌아온다(재생은 그대로).
        editor.RenameRigNode("arrival", "hero", "actor");
        Assert.Equal("art/hero_angry.png", MotionSwaps.ImageAt(Timeline(), "actor", 3.0));
        editor.ExtractRigNodes("arrival", new[] { "actor" }, "cast");
        Assert.Empty(Timeline().Swaps);
        Assert.Equal(2, editor.Document.Find("cast_arrival")!.Swaps.Count);
        Assert.Equal("art/hero_angry.png", MotionSwaps.ImageAt(Timeline(), "cast.actor", 3.0));
        Assert.Equal("art/hero_calm.png", MotionSwaps.ImageAt(Timeline(), "cast.actor", 9.0));

        // 지우기 — 한 칸.
        editor.RemoveImageKey("cast_arrival", 0);
        Assert.Null(MotionSwaps.ImageAt(Timeline(), "cast.actor", 3.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.RemoveImageKey("cast_arrival", 5));
    }

    // ── 컷 파일 — 게임이 읽는 것. 툴의 미리보기와 코어의 재생이 같은 모습을 낸다 ─────

    /// <summary>오브젝트의 키(자리·배율·회전·불투명도) · 카메라의 키 · 깊이의 키 · 그림 키 · 이벤트 · 품은 세트와 재생 블록이 다 든 컷.</summary>
    private static MotionEditor RichCut()
    {
        (MotionEditor editor, CutAsset _) = StageCut();
        KeyAsset K(float t, float v) => new() { T = t, V = v };

        editor.AddSkin("arrival", "arrival");
        editor.SetSkinPart("arrival", "hero", "art/hero_calm.png");
        editor.SetSkinPart("arrival", "sky", "art/sky.png");

        editor.AddChannel("arrival", "hero", MotionChannelKind.Position);
        editor.SetKeys("arrival", "hero", MotionChannelKind.Position, Axis.X, new[] { K(0f, -400f), K(1.5f, 0f), K(6f, 120f) });
        editor.AddKey("arrival", "hero", MotionChannelKind.Position, Axis.Y, K(3f, 40f)); // 하나뿐인 키
        editor.AddChannel("arrival", "hero", MotionChannelKind.Scale);
        editor.SetKeys("arrival", "hero", MotionChannelKind.Scale, Axis.X, new[] { K(2f, 1f), K(4f, 1.5f) });
        editor.AddChannel("arrival", "arm", MotionChannelKind.Euler);
        editor.SetKeys("arrival", "arm", MotionChannelKind.Euler, Axis.Z, new[] { K(0.5f, 0f), K(2.5f, 90f), K(8f, -30f) });
        editor.AddChannel("arrival", "near", MotionChannelKind.Alpha);
        editor.SetKeys("arrival", "near", MotionChannelKind.Alpha, Axis.X, new[] { K(0f, 0f), K(1f, 1f) });

        editor.SetCutCamera("arrival", new CameraSettings { PanY = 20f });
        editor.SetCutCameraKey("arrival", 1f, new CameraSettings { PanY = 20f });
        editor.SetCutCameraKey("arrival", 5f, new CameraSettings { PanX = 240f, PanY = 20f, Zoom = 4f });
        editor.SetCutDepthKey("arrival", "hero", 2f, 14f);
        editor.SetCutDepthKey("arrival", "hero", 7f, 2f);
        editor.AddImageKey("arrival", "hero", 4f, "art/hero_smile.png");
        editor.AddMotionEvent("arrival", 2.5f, "sfx", "pop");

        // 묶음 — 세트 prop(Body 밑에 Tip)과 그 움직임(Body 회전 0 → 45)을 컷에 품고 1초부터 두 번 튼다.
        editor.AddRig(new RigAsset { Kind = "prop", Nodes = { new RigNodeAsset { Id = "Body", Width = 60f, Height = 60f, Image = true, Alpha = true } } });
        editor.AddRigNode("prop", "Tip", "Body", image: true);
        editor.UpdateRigNode("prop", "Tip", new RigNodeAsset { X = 40f, Width = 10f, Height = 10f, Image = true, Alpha = true });
        editor.AddMotion("prop_turn", "prop", 1f);
        editor.AddChannel("prop_turn", "Body", MotionChannelKind.Euler);
        editor.SetKeys("prop_turn", "Body", MotionChannelKind.Euler, Axis.Z, new[] { K(0f, 0f), K(1f, 45f) });
        editor.AddImageKey("prop_turn", "Tip", 0.5f, "art/tip_lit.png");
        editor.AddRigInstance("arrival", "prop", "prop");
        editor.UpdateRigNode("arrival", "prop", new RigNodeAsset { X = -300f, Y = 200f, Alpha = true });
        editor.SetCutNodeDepth("arrival", "prop", 16f);
        editor.AddMotionPlay("arrival", "prop", "prop_turn", 1f, repeat: 2);
        return editor;
    }

    [Fact]
    public void 컷_파일은_펼쳐_구운_한_덩어리이고_프레임으로_적힌다()
    {
        MotionEditor editor = RichCut();
        CutAsset cut = editor.Document.FindCut("arrival")!;
        CutFileDto file = CutFileJson.Build(editor.Document, cut, new Vec2(1920f, 1080f), image => "img/" + image);

        Assert.Equal((CutPlayer.Format, "arrival", 240, 24), (file.format, file.name, file.frames, file.fps));
        Assert.Equal((1920f, 1080f, 20f), (file.stage.width, file.stage.height, file.camera.panY));

        // 오브젝트 — 품은 세트가 경로 이름으로 펼쳐져 있다. 맨 위 오브젝트만 parent가 비고 깊이를 가진다. 그림은 컷 파일 기준 경로.
        Assert.Equal(new[] { "sky", "hero", "arm", "near", "prop", "prop.Body", "prop.Tip" }, file.objects.Select(o => o.id));
        Assert.Equal(new[] { "", "", "hero", "", "", "prop", "prop.Body" }, file.objects.Select(o => o.parent));
        Assert.Equal(new[] { 0f, 14f, 20f, 16f }, file.objects.Where(o => o.parent.Length == 0).Select(o => o.depth));
        Assert.Equal("img/art/hero_calm.png", file.objects.Single(o => o.id == "hero").image);
        Assert.Null(file.objects.Single(o => o.id == "arm").image); // 그림을 안 준 조각

        // 트랙 — 시각은 프레임, 카메라와 깊이는 제 자리로, 재생 블록은 경로 이름의 키로 구워져 있다. 저작본의 점(Camera_Pan · __depth)은 안 보인다.
        CutTrackDto heroX = file.tracks.Single(t => t.target == "hero" && t.what == "x");
        Assert.Equal(new[] { 0f, 36f, 144f }, heroX.keys.Select(k => k.f));
        Assert.Equal(new[] { 72f }, file.tracks.Single(t => t.target == "hero" && t.what == "y").keys.Select(k => k.f));
        Assert.Equal(new[] { 24f, 120f }, file.tracks.Single(t => t.target == CutPlayer.CameraTarget && t.what == "panX").keys.Select(k => k.f));
        Assert.Equal(new[] { 48f, 168f }, file.tracks.Single(t => t.target == "hero" && t.what == "depth").keys.Select(k => k.f));
        // 재생 블록의 뜻: 첫 블록 앞은 가만히 선 값(0), 바퀴마다 처음부터 다시(0 → 45, 쌓이지 않는다 — 이음 0.1프레임), 끝난 뒤로는 마지막 값(45)이 남는다.
        CutTrackDto turn = file.tracks.Single(t => t.target == "prop.Body" && t.what == "rotation");
        Assert.Equal(new[] { (0f, 0f), (24f, 0f), (47.9f, 45f), (48f, 0f), (72f, 45f), (240f, 45f) }, turn.keys.Select(k => (MathF.Round(k.f, 2), k.v))); // 둘째 바퀴는 48프레임에 정확히 — 이음은 그 앞에
        Assert.DoesNotContain(file.tracks, t => t.target.Contains("Camera_", StringComparison.Ordinal) || t.target.EndsWith("__depth", StringComparison.Ordinal));

        // 그림 트랙(계단 키) · 이벤트 — 품은 애니메이션의 그림 키도 바퀴마다 구워져 있다(1초 + 0.5초, 2초 + 0.5초).
        Assert.Equal((96f, "img/art/hero_smile.png"), (file.images.Single(t => t.target == "hero").keys[0].f, file.images.Single(t => t.target == "hero").keys[0].v));
        Assert.Equal(new[] { 36f, 60f }, file.images.Single(t => t.target == "prop.Tip").keys.Select(k => k.f));
        Assert.Equal((60f, "sfx", "pop"), (file.events[0].f, file.events[0].name, file.events[0].value));

        // 글로 적고 다시 읽어도 같다. 모르는 형식은 거절한다.
        string json = CutFileJson.Write(file);
        Assert.Contains("\"format\": \"cut/1\"", json, StringComparison.Ordinal);
        Assert.Equal(json, CutFileJson.Write(CutFileJson.Read(json)));
        Assert.Throws<System.Text.Json.JsonException>(() => CutFileJson.Read(json.Replace("cut/1", "cut/9", StringComparison.Ordinal)));
    }

    [Fact]
    public void 툴의_미리보기와_코어의_컷_재생이_모든_프레임에서_같은_모습을_낸다()
    {
        MotionEditor editor = RichCut();
        CutAsset cut = editor.Document.FindCut("arrival")!;
        var depth = DepthTuningFile.Load(Fixtures.PathOf("presets/depth.json"));
        RigSchemaFile rig = Fixtures.RigOf(editor.Document);
        Vec2 stage = rig.Dto.capturedUnderParentSize.ToVec2();
        MotionAsset timeline = editor.Document.Find("arrival")!;
        rig.TryGetRig("arrival", out RigSchemaRigDto dto);
        SkinAsset skin = RigAssets.EffectiveSkin(editor.Document, "arrival", "arrival")!;

        // 게임이 읽는 길 그대로: 글로 적은 파일을 코어의 DTO로 읽어 코어의 재생기에 넣는다.
        CutFileDto file = CutFileJson.Read(CutFileJson.Write(CutFileJson.Build(editor.Document, cut, stage)));
        Vec3[] probes = { Vec3.Zero, new Vec3(50f, 0f, 0f), new Vec3(0f, 30f, 0f) };

        for (int frame = 0; frame <= cut.Frames; frame += 6)
        {
            double seconds = frame / (double)CutAsset.Fps;

            // 툴: 타임라인을 얹고 카메라와 깊이를 넣는다(뷰포트가 하는 그대로).
            var alphas = new Dictionary<string, float>(StringComparer.Ordinal);
            RectNodeTree tool = MotionPreview.Pose(CutStage.Build(rig, cut, "cut/")!, timeline, seconds, null, "cut/", alphas);
            CutStage.Apply(tool, cut, "cut/", depth);

            // 코어: 컷 파일의 그 프레임.
            CutPose game = CutPlayer.Evaluate(file, frame, depth.Level);

            foreach (CutObjectDto o in file.objects)
            {
                foreach (Vec3 probe in probes)
                {
                    Vec3 a = tool.TransformPoint("cut/" + o.id, probe);
                    Vec3 b = game.Tree.TransformPoint(o.id, probe);
                    Assert.True(MathF.Abs(a.X - b.X) < 0.05f && MathF.Abs(a.Y - b.Y) < 0.05f, $"{o.id} @ {frame}f: 툴 ({a.X}, {a.Y}) · 코어 ({b.X}, {b.Y})");
                }

                float toolAlpha = alphas.TryGetValue("cut/" + o.id, out float alpha) ? alpha : 1f;
                float gameAlpha = game.Alpha.TryGetValue(o.id, out float gameA) ? gameA : 1f;
                Assert.True(MathF.Abs(toolAlpha - gameAlpha) < 1e-3f, $"{o.id} @ {frame}f: 불투명도 {toolAlpha} · {gameAlpha}");

                string? toolImage = MotionSwaps.ImageAt(timeline, o.id, seconds) ?? skin.ImageAt(o.id, 0);
                Assert.Equal(toolImage, game.Image.GetValueOrDefault(o.id));
            }

            Assert.Equal(
                CutStage.DrawOrder(cut, dto.nodes, tool, "cut/").Select(n => n.id).Where(id => id != RigSchemaLoader.RootKey && !CutStage.IsPointNode(id)),
                game.DrawOrder);
        }

        // 이벤트 — 구간 (after, until] 프레임.
        Assert.Equal("sfx", Assert.Single(CutPlayer.EventsBetween(file, 59f, 60f)).name);
        Assert.Empty(CutPlayer.EventsBetween(file, 60f, 240f));
    }
}
