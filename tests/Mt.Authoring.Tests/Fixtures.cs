using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Rig;

namespace Mt.Authoring.Tests;

internal static class Fixtures
{
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>시험용 세트의 이름 — 그래프 산수 · 키 조작 · 도프 시트 · 편집기 명령의 시험이 이 세트의 애니메이션을 고친다.</summary>
    public const string Actor = "actor";

    /// <summary>
    /// 시험용 세트 <c>actor</c>(화면 테스트의 <c>TestDocuments.ActorRig()</c>와 같은 것) — 깊게 품은 묶음들의 사슬 끝에 화면 크기(1920×1080)의 색 사각형 하나.
    /// 묶음마다 자리·회전·배율의 손잡이가 된다(Track_Move_Y는 위아래, SwayPivot은 발 기준 회전). 원점이 그림의 아래 끝(y = −540)에 있는 묶음이 넷이고 나머지는 가운데다.
    /// </summary>
    public static RigAsset ActorRig()
    {
        var rig = new RigAsset { Kind = Actor };
        string parent = "";
        float parentY = 0f;

        foreach (string id in ActorChain)
        {
            float y = FootPivots.Contains(id) ? -540f : 0f;
            rig.Nodes.Add(new RigNodeAsset { Id = id, Parent = parent, Y = y - parentY });
            parent = id;
            parentY = y;
        }

        rig.Nodes.Add(new RigNodeAsset { Id = "CharacterPortraitSprite_Root", Parent = parent, Y = -parentY, Alpha = true });
        rig.Nodes.Add(new RigNodeAsset { Id = "CharacterPortraitSprite_Image", Parent = "CharacterPortraitSprite_Root", Width = 1920f, Height = 1080f, Color = "#3D5A80" });
        rig.Nodes.Add(new RigNodeAsset { Id = "CharacterPortraitSpriteOverlay_Root", Parent = parent, Y = -parentY, Alpha = true });
        rig.Nodes.Add(new RigNodeAsset { Id = "CharacterPortraitSpriteOverlay_Image", Parent = "CharacterPortraitSpriteOverlay_Root" });
        return rig;
    }

    /// <summary>actor의 묶음 사슬 — 앞의 것이 뒤의 것의 부모다.</summary>
    public static readonly string[] ActorChain =
    {
        "CharSlot_Track_Focus", "CharSlot_DepthY", "CharSlot_DepthScale", "CharSlot_Track_Idle", "CharSlot_Track", "CharSlot_Track_X", "CharSlot_Track_Y",
        "CharSlot_Rotation", "CharSlot_SwayPivot", "CharSlot_Scale", "CharacterPortrait_VisualOffset", "CharacterPortrait_Track", "CharacterPortrait_Rotation",
        "CharacterPortrait_Track_Move", "CharacterPortrait_Track_Move_X", "CharacterPortrait_Track_Move_Y", "CharacterPortrait_SwayPivot", "CharacterPortrait_Shake",
        "CharacterPortrait_ActingScale", "CharacterPortrait_ActingScale_X", "CharacterPortrait_ActingScale_Y",
    };

    private static readonly string[] FootPivots = { "CharSlot_DepthScale", "CharSlot_SwayPivot", "CharSlot_Scale", "CharacterPortrait_VisualOffset", "CharacterPortrait_SwayPivot" };

    /// <summary>
    /// 세트 actor와 그 애니메이션 둘을 편집기 명령으로 짓는다.
    /// nod: Track_Move_Y 자리, y 0 → -18 → 0 (0.6초). bow: SwayPivot 회전, z 0 → 8 (0.4초) — x·y는 상수 0의 키다.
    /// </summary>
    public static MotionDocument ActorDocument()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(ActorRig());

        editor.AddMotion("nod", Actor, 0.6f);
        editor.AddChannel("nod", "CharacterPortrait_Track_Move_Y", MotionChannelKind.Position);
        editor.SetKeys("nod", "CharacterPortrait_Track_Move_Y", MotionChannelKind.Position, Axis.Y, new[]
        {
            Key(0f, 0f), Key(0.3f, -18f), Key(0.6f, 0f),
        });

        editor.AddMotion("bow", Actor, 0.4f);
        editor.AddChannel("bow", "CharacterPortrait_SwayPivot", MotionChannelKind.Euler);
        editor.SetKeys("bow", "CharacterPortrait_SwayPivot", MotionChannelKind.Euler, Axis.X, new[] { Key(0f, 0f), Key(0.4f, 0f) });
        editor.SetKeys("bow", "CharacterPortrait_SwayPivot", MotionChannelKind.Euler, Axis.Y, new[] { Key(0f, 0f), Key(0.4f, 0f) });
        editor.SetKeys("bow", "CharacterPortrait_SwayPivot", MotionChannelKind.Euler, Axis.Z, new[] { Key(0f, 0f), Key(0.4f, 8f) });

        return editor.Document;
    }

    /// <summary>문서의 세트·컷을 세운 한 벌(무대 1920×1080) — 앱에서는 세션이 하는 일.</summary>
    public static RigSchemaFile RigOf(MotionDocument document) => RigAssets.Compose(document, new Vec2(1920f, 1080f));

    /// <summary>세트 actor 하나만 세운 한 벌.</summary>
    public static RigSchemaFile ActorRigFile() => RigOf(new MotionDocument { Rigs = { ActorRig() } });

    public static KeyAsset Key(float t, float v, float inTangent = 0f, float outTangent = 0f) => new()
    {
        T = t,
        V = v,
        InTangent = inTangent,
        OutTangent = outTangent,
        Tangent = TangentMode.Flat,
    };
}
