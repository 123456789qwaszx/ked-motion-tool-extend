using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Sets;
using Mt.Authoring.Serialization;

namespace Mt.Authoring.Tests;

/// <summary>스킨과 조각(<c>PLAN-parts.md</c> P1) — 리그의 조각마다 붙일 그림의 묶음.</summary>
public class SkinTests
{
    private static readonly BuiltSets BuiltSets = Fixtures.SetOf(new AuthoringDocument { Sets = { PartsSetFixture.Set(), Fixtures.ActorSet() } });

    private static DocumentEditor NewEditor() => new(new AuthoringDocument());

    [Fact]
    public void 조각은_오브젝트의_그림_표시로_가리고_그림이_없는_세트에는_없다()
    {
        Assert.Equal(new[] { "Badge_Part1", "Badge_Part2", "Badge_Part3", "Badge_Part4" }, ObjectParts.Of(BuiltSets, PartsSetFixture.SetName));
        Assert.Empty(ObjectParts.Of(BuiltSets, Fixtures.Actor)); // 색 사각형뿐 — 그림을 그리는 오브젝트가 없다
        Assert.Empty(ObjectParts.Of(BuiltSets, "nope"));
        Assert.Empty(ObjectParts.Of(null, PartsSetFixture.SetName));

        // 이름이 아니라 표시를 본다 — 이름이 조각 같아도 "그림"이 꺼져 있으면 조각이 아니다.
        SetAsset unmarked = PartsSetFixture.Set("plain", parts: 2);
        unmarked.Objects.ForEach(n => n.Image = false);
        Assert.Empty(ObjectParts.Of(Fixtures.SetOf(new AuthoringDocument { Sets = { unmarked } }), "plain"));

        // 조각 노드는 리그의 보통 노드다 — 트리에 서고, 모션의 채널이 된다.
        RectNodeTree tree = BuiltSets.BuildTree(PartsSetFixture.SetName, "e1/");
        Assert.True(tree.Contains("e1/Badge_Part3"));
        Assert.Equal(PartsSetFixture.PartSize, tree.GetRectSize("e1/Badge_Part1").X, 3);
        Assert.Contains("Badge_Part1", BuiltSets.NodeIds[PartsSetFixture.SetName]);
    }

    [Fact]
    public void 스킨은_저작본에_왕복하고_없으면_적지_않는다()
    {
        DocumentEditor editor = NewEditor();
        Assert.DoesNotContain("skins", AuthoringDocumentJson.Write(editor.Document), StringComparison.Ordinal);

        editor.AddSkin("꽃", PartsSetFixture.SetName);
        editor.SetSkinPart("꽃", "Badge_Part2", "art/꽃-작은.png");
        editor.SetSkinPart("꽃", "Badge_Part1", "art/꽃-큰.png");

        string json = AuthoringDocumentJson.Write(editor.Document);
        AuthoringDocument read = AuthoringDocumentJson.Read(json);
        Assert.Equal(json, AuthoringDocumentJson.Write(read));
        SkinAsset skin = Assert.Single(read.Skins);
        Assert.Equal(("꽃", PartsSetFixture.SetName), (skin.Name, skin.Set));
        Assert.Equal(new[] { ("Badge_Part1", "art/꽃-큰.png"), ("Badge_Part2", "art/꽃-작은.png") }, skin.Parts.Select(p => (p.Key, p.Value))); // 노드 이름순
    }

    [Fact]
    public void 명령은_한_칸이고_이름을_바꾸면_그_스킨을_입힌_인스턴스가_따라온다()
    {
        DocumentEditor editor = NewEditor();
        editor.AddSet(PartsSetFixture.Set());
        editor.AddSet(new SetAsset { Name = "shelf", Objects = { new ObjectAsset { Id = "left", Set = PartsSetFixture.SetName }, new ObjectAsset { Id = "right", Set = PartsSetFixture.SetName } } });
        editor.AddSkin("  sweat ", PartsSetFixture.SetName); // 앞뒤 빈칸은 버린다
        editor.SetInstanceSkin("shelf", "left", "sweat");
        editor.SetInstanceSkin("shelf", "right", "sweat");
        string?[] Worn() => editor.Document.FindSetOrCut("shelf")!.Objects.Select(n => n.SetSkin).ToArray();

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
        DocumentEditor editor = NewEditor();
        editor.AddSkin("a", PartsSetFixture.SetName);
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddSkin("a", PartsSetFixture.SetName));
        Assert.Throws<InvalidOperationException>(() => editor.AddSkin("  ", PartsSetFixture.SetName));
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
        DocumentEditor editor = NewEditor();
        editor.AddSkin("땀", PartsSetFixture.SetName);
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
        string json = AuthoringDocumentJson.Write(editor.Document);
        Assert.Contains("\"setups\"", json, StringComparison.Ordinal);
        Assert.Equal(json, AuthoringDocumentJson.Write(AuthoringDocumentJson.Read(json)));
        Assert.Equal("c.png", AuthoringDocumentJson.Read(json).FindSkin("땀")!.ImageAt("Badge_Part1", 99));

        // 한 장으로 줄이고 기본값으로 돌리면 덧붙임이 사라진다. 그림을 비우면 덧붙임도 함께.
        editor.SetSkinPartImages("땀", "Badge_Part1", new[] { "a.png" });
        editor.SetSkinPartSetup("땀", "Badge_Part1", new SkinPartSetup());
        Assert.Empty(skin.Setups);
        Assert.DoesNotContain("\"setups\"", AuthoringDocumentJson.Write(editor.Document), StringComparison.Ordinal);
        editor.SetSkinPartSetup("땀", "Badge_Part1", new SkinPartSetup { X = 30f });
        editor.SetSkinPart("땀", "Badge_Part1", null);
        Assert.Empty(skin.Setups);
    }
}
