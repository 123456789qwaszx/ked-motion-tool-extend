using Ked.Presentation.Core;
using Mt.Authoring.Model;
using Mt.Authoring.Sets;

namespace Mt.Authoring.Tests;

public class BuiltSetsTests
{
    [Fact]
    public void 세운_한_벌에서_세트와_노드_목록이_나온다_하드코딩된_이름은_없다()
    {
        var document = new AuthoringDocument { Sets = { Fixtures.ActorSet(), PartsSetFixture.Set() } };
        BuiltSets set = Fixtures.BuiltOf(document);

        Assert.Contains(Fixtures.Actor, set.SetNames);
        Assert.Contains(PartsSetFixture.SetName, set.SetNames);
        Assert.DoesNotContain("background", set.SetNames);
        Assert.Contains("CharacterPortrait_Track_Move_Y", set.NodeIds[Fixtures.Actor]);
        Assert.Contains("CharacterPortrait_SwayPivot", set.NodeIds[Fixtures.Actor]);
        Assert.DoesNotContain("CharacterPortrait_SwayPivot", set.NodeIds[PartsSetFixture.SetName]);
        Assert.True(set.TryGetObjects(Fixtures.Actor, out IReadOnlyList<ExpandedObject> actor));
        Assert.Equal(25, actor.Count); // 루트는 목록에 없다 — 트리를 세울 때만 붙는다
        Assert.False(set.TryGetObjects("nope", out _));
        Assert.Throws<KeyNotFoundException>(() => set.BuildTree("nope"));
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
    public void 부모가_앞에_없는_계층은_세우지_못한다()
    {
        var objects = new List<ExpandedObject>
        {
            new("child", "parent", Vec2.Zero, new Vec2(10f, 10f), Vec2.One, 0f, false, false, null),
            new("parent", "", Vec2.Zero, new Vec2(10f, 10f), Vec2.One, 0f, false, false, null),
        };

        // 조용히 반쪽으로 서는 대신 던진다 — 세션은 빈 한 벌로 물러나고 사람에게 알린다.
        Assert.ThrowsAny<Exception>(() => BuiltSets.Of(new Vec2(1920f, 1080f), new[] { ("bad", objects) }));
    }

    [Fact]
    public void 빈_한_벌은_세트가_없고_무대_크기만_안다()
    {
        BuiltSets empty = BuiltSets.Empty(new Vec2(1920f, 1080f));

        Assert.Empty(empty.SetNames);
        Assert.Equal(new Vec2(1920f, 1080f), empty.StageSize);
    }
}
