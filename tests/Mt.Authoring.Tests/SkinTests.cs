using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;
using Mt.Authoring.Serialization;

namespace Mt.Authoring.Tests;

/// <summary>스킨과 조각(<c>PLAN-parts.md</c> P1) — 리그의 조각마다 붙일 그림의 묶음.</summary>
public class SkinTests
{
    private static readonly RigSchemaFile RigFile = RigSchemaFile.Load(PartsRigFixture.WriteDump(Fixtures.PathOf("rig-schemas.json")));

    private static MotionEditor NewEditor() => new(new MotionDocument());

    [Fact]
    public void 조각은_이름_규칙으로_가리고_캐릭터에는_없다()
    {
        Assert.Equal(new[] { "Badge_Part1", "Badge_Part2", "Badge_Part3", "Badge_Part4" }, RigParts.Of(RigFile, PartsRigFixture.RigKind));
        Assert.Empty(RigParts.Of(RigFile, "character"));
        Assert.Empty(RigParts.Of(RigFile, "nope"));
        Assert.Empty(RigParts.Of(null, PartsRigFixture.RigKind));
        Assert.True(RigParts.IsPartNode("Badge_Part12"));
        Assert.False(RigParts.IsPartNode("Badge_Body"));
        Assert.False(RigParts.IsPartNode("Part1_Root"));

        // 조각 노드는 리그의 보통 노드다 — 트리에 서고, 모션의 채널이 된다.
        RectNodeTree tree = RigFile.BuildTree(PartsRigFixture.RigKind, "e1/");
        Assert.True(tree.Contains("e1/Badge_Part3"));
        Assert.Equal(PartsRigFixture.PartSize, tree.GetRectSize("e1/Badge_Part1").X, 3);
        Assert.Contains("Badge_Part1", RigFile.NodeIds[PartsRigFixture.RigKind]);
    }

    [Fact]
    public void 스킨은_저작본에_왕복하고_없으면_적지_않는다()
    {
        MotionEditor editor = NewEditor();
        Assert.DoesNotContain("skins", MotionDocumentJson.Write(editor.Document), StringComparison.Ordinal);

        editor.AddSkin("꽃", PartsRigFixture.RigKind);
        editor.SetSkinPart("꽃", "Badge_Part2", "art/꽃-작은.png");
        editor.SetSkinPart("꽃", "Badge_Part1", "art/꽃-큰.png");

        string json = MotionDocumentJson.Write(editor.Document);
        MotionDocument read = MotionDocumentJson.Read(json);
        Assert.Equal(json, MotionDocumentJson.Write(read));
        SkinAsset skin = Assert.Single(read.Skins);
        Assert.Equal(("꽃", PartsRigFixture.RigKind), (skin.Name, skin.Rig));
        Assert.Equal(new[] { ("Badge_Part1", "art/꽃-큰.png"), ("Badge_Part2", "art/꽃-작은.png") }, skin.Parts.Select(p => (p.Key, p.Value))); // 노드 이름순
    }

    [Fact]
    public void 명령은_한_칸이고_이름을_바꾸면_그_스킨을_입힌_인스턴스가_따라온다()
    {
        MotionEditor editor = NewEditor();
        editor.AddRig(PartsRigFixture.Rig());
        editor.AddRig(new RigAsset { Kind = "shelf", Nodes = { new RigNodeAsset { Id = "left", Set = PartsRigFixture.RigKind }, new RigNodeAsset { Id = "right", Set = PartsRigFixture.RigKind } } });
        editor.AddSkin("  sweat ", PartsRigFixture.RigKind); // 앞뒤 빈칸은 버린다
        editor.SetRigInstanceSkin("shelf", "left", "sweat");
        editor.SetRigInstanceSkin("shelf", "right", "sweat");
        string?[] Worn() => editor.Document.FindRig("shelf")!.Nodes.Select(n => n.SetSkin).ToArray();

        long revision = editor.Revision;
        editor.RenameSkin("sweat", "땀");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { "땀", "땀" }, Worn());
        editor.Undo();
        Assert.Equal(new[] { "sweat", "sweat" }, Worn());
        Assert.NotNull(editor.Document.FindSkin("sweat"));
        editor.Redo();

        // 조각 그림: 넣고, 같은 값은 흔적이 없고, 비우면 빠진다.
        editor.SetSkinPart("땀", "Badge_Part1", "a.png");
        revision = editor.Revision;
        editor.SetSkinPart("땀", "Badge_Part1", "a.png");
        editor.SetSkinPart("땀", "Badge_Part3", "");
        Assert.Equal(revision, editor.Revision);
        editor.SetSkinPart("땀", "Badge_Part1", null);
        Assert.Empty(editor.Document.FindSkin("땀")!.Parts);

        // 지우기도 한 칸 — 되돌리면 돌아온다.
        revision = editor.Revision;
        editor.RemoveSkin("땀");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Null(editor.Document.FindSkin("땀"));
        editor.Undo();
        Assert.NotNull(editor.Document.FindSkin("땀"));
    }

    [Fact]
    public void 이름과_리그가_안_맞으면_막는다()
    {
        MotionEditor editor = NewEditor();
        editor.AddSkin("a", PartsRigFixture.RigKind);
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddSkin("a", PartsRigFixture.RigKind));
        Assert.Throws<InvalidOperationException>(() => editor.AddSkin("  ", PartsRigFixture.RigKind));
        Assert.Throws<InvalidOperationException>(() => editor.AddSkin("b", ""));
        Assert.Throws<KeyNotFoundException>(() => editor.SetSkinPart("nope", "Badge_Part1", "x.png"));
        Assert.Throws<InvalidOperationException>(() => editor.SetSkinPart("a", "", "x.png"));
        Assert.Throws<KeyNotFoundException>(() => editor.RemoveSkin("nope"));
        Assert.Equal(revision, editor.Revision);

        editor.RenameSkin("a", " a "); // 같은 이름 — 아무 일도 없다
        Assert.Equal(revision, editor.Revision);
    }

    // ── 조각의 덧붙임(PLAN-parts.md §13) — 그림 넘기기 · 놓임 ────

    [Fact]
    public void 조각에_장을_여럿_주면_장당_시간마다_넘어가고_되풀이하거나_마지막에서_멈춘다()
    {
        MotionEditor editor = NewEditor();
        editor.AddSkin("땀", PartsRigFixture.RigKind);
        long revision = editor.Revision;

        // 장들을 한 번에 — 한 칸. 첫 장이 그 조각의 그림이다. 빈 것은 버린다.
        editor.SetSkinPartImages("땀", "Badge_Part1", new[] { " a.png ", "b.png", "", "c.png" });
        editor.SetSkinPartImages("땀", "Badge_Part1", new[] { "a.png", "b.png", "c.png" }); // 같은 값은 무기록
        Assert.Equal(revision + 1, editor.Revision);
        SkinAsset skin = editor.Document.FindSkin("땀")!;
        Assert.Equal("a.png", skin.Parts["Badge_Part1"]);
        Assert.Equal(new[] { "a.png", "b.png", "c.png" }, skin.ImagesOf("Badge_Part1"));
        Assert.Equal(new[] { "a.png", "b.png", "c.png" }, skin.AllImages());

        // 기본: 장당 0.2초, 되풀이.
        Assert.Equal("a.png", skin.ImageAt("Badge_Part1", 0));
        Assert.Equal("a.png", skin.ImageAt("Badge_Part1", 0.19));
        Assert.Equal("b.png", skin.ImageAt("Badge_Part1", 0.2));
        Assert.Equal("c.png", skin.ImageAt("Badge_Part1", 0.45));
        Assert.Equal("a.png", skin.ImageAt("Badge_Part1", 0.6));
        Assert.Equal("a.png", skin.ImageAt("Badge_Part1", -1)); // 뜨기 전
        Assert.Null(skin.ImageAt("Badge_Part2", 0));

        // 장당 시간과 되풀이 — 끄면 마지막 장에서 멈춘다.
        SkinPartSetup setup = skin.SetupOf("Badge_Part1").Clone();
        setup.FrameSeconds = 0.5f;
        setup.Loop = false;
        editor.SetSkinPartSetup("땀", "Badge_Part1", setup);
        Assert.Equal("b.png", skin.ImageAt("Badge_Part1", 0.7));
        Assert.Equal("c.png", skin.ImageAt("Badge_Part1", 99));

        // 막는 것.
        revision = editor.Revision;
        Assert.Throws<InvalidOperationException>(() => editor.SetSkinPartSetup("땀", "Badge_Part2", new SkinPartSetup { X = 1f })); // 그림이 없는 조각
        Assert.Throws<InvalidOperationException>(() => editor.SetSkinPartSetup("땀", "Badge_Part1", new SkinPartSetup { Scale = 0f }));
        Assert.Throws<InvalidOperationException>(() => editor.SetSkinPartSetup("땀", "Badge_Part1", new SkinPartSetup { FrameSeconds = 0f }));
        Assert.Equal(revision, editor.Revision);

        // 저작본 왕복. 덧붙임이 없는 스킨에는 안 적힌다.
        string json = MotionDocumentJson.Write(editor.Document);
        Assert.Contains("\"setups\"", json, StringComparison.Ordinal);
        Assert.Equal(json, MotionDocumentJson.Write(MotionDocumentJson.Read(json)));
        Assert.Equal("c.png", MotionDocumentJson.Read(json).FindSkin("땀")!.ImageAt("Badge_Part1", 99));

        // 한 장으로 줄이고 기본값으로 돌리면 덧붙임이 사라진다. 그림을 비우면 덧붙임도 함께.
        editor.SetSkinPartImages("땀", "Badge_Part1", new[] { "a.png" });
        editor.SetSkinPartSetup("땀", "Badge_Part1", new SkinPartSetup());
        Assert.Empty(skin.Setups);
        Assert.DoesNotContain("\"setups\"", MotionDocumentJson.Write(editor.Document), StringComparison.Ordinal);
        editor.SetSkinPartSetup("땀", "Badge_Part1", new SkinPartSetup { X = 30f });
        editor.SetSkinPart("땀", "Badge_Part1", null);
        Assert.Empty(skin.Setups);
    }
}
