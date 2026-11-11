using Mt.App.Views;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Sets;

namespace Mt.App.Tests;

internal static class TestDocuments
{
    public static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>그림 견본(<c>Fixtures/art</c>) — 층 표 <c>layers.tsv</c>와 그 표의 PNG 넷(sign · microbe_sun · microbe_leaf · microbe_cell).</summary>
    public static string ArtPath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "art", name);

    /// <summary>시험용 세트의 이름 — 그래프·도프 시트·타임 슬라이더·뷰포트의 시험이 이 세트의 애니메이션을 고친다.</summary>
    public const string Actor = "actor";

    /// <summary>
    /// 시험용 세트 <c>actor</c> — 깊게 품은 묶음들의 사슬 끝에 화면 크기(1920×1080)의 색 사각형 하나(<c>…Sprite_Image</c>).
    /// 묶음마다 자리·회전·배율의 손잡이가 된다(Track_Move_Y는 위아래, SwayPivot은 발 기준 회전, ActingScale은 배율).
    /// 원점이 그림의 아래 끝(y = −540)에 있는 묶음이 넷(<see cref="FootPivots"/>)이고 나머지는 가운데다 — 오브젝트가 25개라 그래프 목록의 "키 없음" 줄(16개까지)이 서지 않는다.
    /// </summary>
    public static SetAsset ActorSet()
    {
        var set = new SetAsset { Name = Actor };
        string parent = "";
        float parentY = 0f;

        foreach (string id in ActorChain)
        {
            float y = FootPivots.Contains(id) ? -540f : 0f;
            set.Objects.Add(new ObjectAsset { Id = id, Parent = parent, Y = y - parentY });
            parent = id;
            parentY = y;
        }

        set.Objects.Add(new ObjectAsset { Id = "CharacterPortraitSprite_Root", Parent = parent, Y = -parentY, Alpha = true });
        set.Objects.Add(new ObjectAsset { Id = ActorImage, Parent = "CharacterPortraitSprite_Root", Width = 1920f, Height = 1080f, Color = "#3D5A80" });
        set.Objects.Add(new ObjectAsset { Id = "CharacterPortraitSpriteOverlay_Root", Parent = parent, Y = -parentY, Alpha = true });
        set.Objects.Add(new ObjectAsset { Id = "CharacterPortraitSpriteOverlay_Image", Parent = "CharacterPortraitSpriteOverlay_Root" });
        return set;
    }

    /// <summary>actor의 그림(색 사각형) — 뷰포트의 대표 사각형이 이것이다.</summary>
    public const string ActorImage = "CharacterPortraitSprite_Image";

    /// <summary>actor의 묶음 사슬 — 앞의 것이 뒤의 것의 부모다.</summary>
    public static readonly string[] ActorChain =
    {
        "CharSlot_Track_Focus", "CharSlot_DepthY", "CharSlot_DepthScale", "CharSlot_Track_Idle", "CharSlot_Track", "CharSlot_Track_X", "CharSlot_Track_Y",
        "CharSlot_Rotation", "CharSlot_SwayPivot", "CharSlot_Scale", "CharacterPortrait_VisualOffset", "CharacterPortrait_Track", "CharacterPortrait_Rotation",
        "CharacterPortrait_Track_Move", "CharacterPortrait_Track_Move_X", "CharacterPortrait_Track_Move_Y", "CharacterPortrait_SwayPivot", "CharacterPortrait_Shake",
        "CharacterPortrait_ActingScale", "CharacterPortrait_ActingScale_X", "CharacterPortrait_ActingScale_Y",
    };

    private static readonly string[] FootPivots = { "CharSlot_DepthScale", "CharSlot_SwayPivot", "CharSlot_Scale", "CharacterPortrait_VisualOffset", "CharacterPortrait_SwayPivot" };

    /// <summary>세트 actor와 그 애니메이션 nod(끄덕임): Track_Move_Y 자리, y 0 → -18 → 0 (0.6초).</summary>
    public static DocumentEditor Nod()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(ActorSet());
        editor.AddTimeline("nod", Actor, 0.6f);
        editor.AddChannel("nod", "CharacterPortrait_Track_Move_Y", ChannelKind.Position);
        editor.SetKeys("nod", "CharacterPortrait_Track_Move_Y", ChannelKind.Position, Axis.Y, new[]
        {
            Key(0f, 0f), Key(0.3f, -18f), Key(0.6f, 0f),
        });
        return editor;
    }

    /// <summary>뷰포트를 따로 세워 시험할 때 — 문서의 세트·컷을 세운 리그를 싣고 그 세트를 연다(창에서는 세션이 하는 일).</summary>
    public static void Open(ViewportView view, DocumentEditor editor, string kind = Actor)
    {
        view.SetBuilt(SetBuilder.Compose(editor.Document, BaseResolutionFile.Default));
        view.SetView = kind;
    }

    /// <summary>창에 세트 actor를 만들어 연다 — 그 뒤의 "모션 추가"가 이 세트의 애니메이션이 된다.</summary>
    public static void OpenActor(MainWindow window)
    {
        window.Session.Editor.AddSet(ActorSet());
        window.SelectSet(Actor);
    }

    public static KeyAsset Key(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Flat };
}
