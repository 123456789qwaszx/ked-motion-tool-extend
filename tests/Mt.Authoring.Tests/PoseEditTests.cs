using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Graph;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Sets;

namespace Mt.Authoring.Tests;

public class PoseEditTests
{
    private static readonly BuiltSets BuiltSets = Fixtures.ActorBuiltSets();

    [Fact]
    public void 이동은_부모_공간_변위다_부모가_배율되면_화면_변위와_다르다()
    {
        RectNodeTree tree = BuiltSets.BuildTree(Fixtures.Actor);
        const string node = "CharacterPortrait_Track_Move_Y";

        Vec2 plain = PoseEdit.RootDeltaToLocal(tree, node, new Vec2(0f, 0f), new Vec2(10f, -18f));
        Assert.Equal(10f, plain.X, 3);
        Assert.Equal(-18f, plain.Y, 3);

        // 조상(CharSlot_Scale)을 2배로 — 화면 20px가 로컬 10이다.
        tree.SetState("CharSlot_Scale", tree.GetState("CharSlot_Scale").WithLocalScale(new Vec3(2f, 2f, 1f)));
        Vec2 scaled = PoseEdit.RootDeltaToLocal(tree, node, new Vec2(0f, 0f), new Vec2(20f, 0f));
        Assert.Equal(10f, scaled.X, 3);

        Assert.Equal(new Vec2(3f, 4f), PoseEdit.RootDeltaToLocal(tree, "__root", new Vec2(1f, 1f), new Vec2(4f, 5f)));
    }

    [Fact]
    public void 회전은_반시계_양수이고_감긴다_배율은_팔_방향_투영이라_가운데를_넘어도_뒤집히지_않는다()
    {
        var pivot = new Vec2(0f, 0f);
        Assert.Equal(90f, PoseEdit.AngleDelta(pivot, new Vec2(1f, 0f), new Vec2(0f, 1f)), 3);
        Assert.Equal(-90f, PoseEdit.AngleDelta(pivot, new Vec2(0f, 1f), new Vec2(1f, 0f)), 3);
        Assert.Equal(-170f, PoseEdit.AngleDelta(pivot, new Vec2(1f, 0f), new Vec2(-0.9848f, -0.1736f)), 1);

        var alongX = new Vec2(3f, 0f); // 방향만 본다(길이 무관)
        Assert.Equal(2f, PoseEdit.ScaleAlong(alongX, new Vec2(60f, 0f), new Vec2(120f, 0f), 60f), 4); // 팔 하나만큼 밖 = ×2
        Assert.Equal(0.5f, PoseEdit.ScaleAlong(alongX, new Vec2(60f, 0f), new Vec2(30f, 0f), 60f), 4); // 반 팔 안 = ×0.5
        Assert.Equal(0.01f, PoseEdit.ScaleAlong(alongX, new Vec2(60f, 0f), new Vec2(-60f, 0f), 60f), 4); // 가운데를 지나도 바닥에서 멈춘다
        Assert.Equal(1f, PoseEdit.ScaleAlong(alongX, new Vec2(60f, 0f), new Vec2(60f, 40f), 60f), 4); // 직각은 1
        Assert.Equal(1f, PoseEdit.ScaleAlong(Vec2.Zero, new Vec2(60f, 0f), new Vec2(0f, 0f), 60f), 4);
    }

    [Fact]
    public void 키_값은_포즈의_값_그대로다_채널_종류의_자리에서_읽고_쓴다()
    {
        var state = new RectNodeState(new Vec2(1f, 2f), Vec2.Zero, Vec2.One, Vec2.Half, Vec2.Zero, new Vec3(3f, 4f, 5f), new Vec3(6f, 7f, 8f));
        Assert.Equal(2f, PoseEdit.Component(ChannelKind.Position, state, Axis.Y));
        Assert.Equal(4f, PoseEdit.Component(ChannelKind.Scale, state, Axis.Y));
        Assert.Equal(8f, PoseEdit.Component(ChannelKind.Euler, state, Axis.Z));

        // 값 칸에 넣은 수가 그 자리로 들어가고, 다시 읽으면 그 수다(키로 놓이는 값).
        Assert.Equal(-18f, PoseEdit.Component(ChannelKind.Position, PoseEdit.WithComponent(ChannelKind.Position, state, Axis.Y, -18f), Axis.Y));
        Assert.Equal(1.5f, PoseEdit.Component(ChannelKind.Scale, PoseEdit.WithComponent(ChannelKind.Scale, state, Axis.X, 1.5f), Axis.X));
        Assert.Equal(10f, PoseEdit.Component(ChannelKind.Euler, PoseEdit.WithComponent(ChannelKind.Euler, state, Axis.Z, 10f), Axis.Z));
    }

    [Fact]
    public void 포즈_키는_채널과_성분을_만들고_같은_시각을_바꾸며_한_번의_조작이다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddTimeline("a", Fixtures.Actor, 1f);
        long before = editor.Revision;

        List<KeyRef> keys = editor.KeyComponents("a", "N", ChannelKind.Position, 0.5f, new[] { (Axis.X, 10f), (Axis.Y, 20f) }, TangentMode.Auto);

        Assert.Equal(before + 1, editor.Revision);
        Assert.Equal(2, keys.Count);
        ChannelAsset channel = editor.Document.Find("a")!.FindChannel("N", ChannelKind.Position)!;
        Assert.Equal(20f, channel.Y!.Keys[0].V);

        editor.KeyComponents("a", "N", ChannelKind.Position, 0.5f, new[] { (Axis.Y, 25f) }, TangentMode.Auto);
        Assert.Single(channel.Y.Keys);
        Assert.Equal(25f, editor.Document.Find("a")!.FindChannel("N", ChannelKind.Position)!.Y!.Keys[0].V);

        editor.Undo();
        editor.Undo();
        Assert.Null(editor.Document.Find("a")!.FindChannel("N", ChannelKind.Position));
    }
}
