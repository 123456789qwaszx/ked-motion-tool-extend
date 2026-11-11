using Ked.Presentation.Core;
using Mt.Authoring.Model;
using Mt.Authoring.Sets;

namespace Mt.Authoring.Tests;

public class BuiltSetsTests
{
    [Fact]
    public void 세운_한_벌에서_리그와_노드_목록이_나온다_하드코딩된_이름은_없다()
    {
        var document = new AuthoringDocument { Sets = { Fixtures.ActorSet(), PartsSetFixture.Set() } };
        BuiltSets set = Fixtures.SetOf(document);

        Assert.Contains(Fixtures.Actor, set.SetNames);
        Assert.Contains(PartsSetFixture.SetName, set.SetNames);
        Assert.DoesNotContain("background", set.SetNames);
        Assert.Contains("CharacterPortrait_Track_Move_Y", set.NodeIds[Fixtures.Actor]);
        Assert.Contains("CharacterPortrait_SwayPivot", set.NodeIds[Fixtures.Actor]);
        Assert.DoesNotContain("CharacterPortrait_SwayPivot", set.NodeIds[PartsSetFixture.SetName]);
        Assert.True(set.TryGetSet(Fixtures.Actor, out HierarchyDto actor));
        Assert.Equal(26, actor.nodes.Count); // 오브젝트 25 + 루트
        Assert.False(set.TryGetSet("nope", out _));
    }

    [Fact]
    public void 코어_트리로_세워지고_시작값은_오브젝트의_것이다()
    {
        BuiltSets set = Fixtures.ActorBuiltSets();

        RectNodeTree tree = set.BuildTree(Fixtures.Actor, "c1/");
        RectNodeState sway = tree.GetState("c1/CharacterPortrait_SwayPivot");

        Assert.Equal(1f, sway.LocalScale.X);
        Assert.Equal(-540f, sway.AnchoredPosition.Y); // 원점이 그림의 아래 끝에 있는 묶음(부모 Track_Move_Y는 가운데)
        Assert.Throws<KeyNotFoundException>(() => tree.GetState("CharacterPortrait_SwayPivot"));
    }

    [Fact]
    public void 빈_한_벌은_리그가_없고_무대_크기만_안다()
    {
        BuiltSets empty = BuiltSets.Empty(new Vec2(1920f, 1080f));

        Assert.Empty(empty.SetNames);
        Assert.Equal(1920f, empty.Dto.stageSize.x);
        Assert.Equal(1080f, empty.Dto.stageSize.y);
    }
}
