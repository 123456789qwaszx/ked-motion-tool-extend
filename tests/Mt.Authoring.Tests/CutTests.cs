using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Serialization;
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
}
