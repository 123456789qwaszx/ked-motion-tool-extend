using Ked.Presentation.Core;
using Mt.Authoring.Model;
using Mt.Authoring.Rig;

namespace Mt.Authoring.Tests;

public class RigSchemaFileTests
{
    [Fact]
    public void 세운_한_벌에서_리그와_노드_목록이_나온다_하드코딩된_이름은_없다()
    {
        var document = new MotionDocument { Rigs = { Fixtures.ActorRig(), PartsRigFixture.Rig() } };
        RigSchemaFile rig = Fixtures.RigOf(document);

        Assert.Contains(Fixtures.Actor, rig.RigKinds);
        Assert.Contains(PartsRigFixture.RigKind, rig.RigKinds);
        Assert.DoesNotContain("background", rig.RigKinds);
        Assert.Contains("CharacterPortrait_Track_Move_Y", rig.NodeIds[Fixtures.Actor]);
        Assert.Contains("CharacterPortrait_SwayPivot", rig.NodeIds[Fixtures.Actor]);
        Assert.DoesNotContain("CharacterPortrait_SwayPivot", rig.NodeIds[PartsRigFixture.RigKind]);
        Assert.True(rig.TryGetRig(Fixtures.Actor, out HierarchyDto actor));
        Assert.Equal(26, actor.nodes.Count); // 오브젝트 25 + 루트
        Assert.False(rig.TryGetRig("nope", out _));
    }

    [Fact]
    public void 코어_트리로_세워지고_시작값은_오브젝트의_것이다()
    {
        RigSchemaFile rig = Fixtures.ActorRigFile();

        RectNodeTree tree = rig.BuildTree(Fixtures.Actor, "c1/");
        RectNodeState sway = tree.GetState("c1/CharacterPortrait_SwayPivot");

        Assert.Equal(1f, sway.LocalScale.X);
        Assert.Equal(-540f, sway.AnchoredPosition.Y); // 원점이 그림의 아래 끝에 있는 묶음(부모 Track_Move_Y는 가운데)
        Assert.Throws<KeyNotFoundException>(() => tree.GetState("CharacterPortrait_SwayPivot"));
    }

    [Fact]
    public void 빈_한_벌은_리그가_없고_무대_크기만_안다()
    {
        RigSchemaFile empty = RigSchemaFile.Empty(new Vec2(1920f, 1080f));

        Assert.Empty(empty.RigKinds);
        Assert.Equal(1920f, empty.Dto.stageSize.x);
        Assert.Equal(1080f, empty.Dto.stageSize.y);
    }
}
