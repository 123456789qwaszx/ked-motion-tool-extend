using Ked.Presentation.Core;

namespace Mt.Authoring.Model;

/// <summary>
/// 저작 원본 한 벌 (<c>*.cut-authoring.json</c>, 스키마 <see cref="AuthoringFormat.AuthoringSchema"/>).
///
/// 컷 파일(<c>cut/1</c>)에 구워져 나가기 전의 것 — 세트의 참조 · 재생 블록 · 탄젠트 모드와 끊김 — 을 함께 든다.
///
/// 전부 가변 클래스다. 편집은 <see cref="Editing.DocumentEditor"/>만 하고, 되돌리기는 문서 전체 스냅샷이다
/// (VnTool <c>ProjectEditor</c>의 방식) — 명령마다 역연산을 짓지 않는다.
/// </summary>
public sealed class AuthoringDocument
{
    public string Schema { get; set; } = AuthoringFormat.AuthoringSchema;

    public List<TimelineAsset> Timelines { get; set; } = new();

    /// <summary>
    /// 스킨들 — 세트의 조각마다 붙일 그림의 묶음. 미리보기 전용.
    /// 저작본에는 <see cref="SkinsOrNull"/>로 실린다: 스킨이 없는 문서는 전과 같은 글자다.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<SkinAsset> Skins { get; set; } = new();

    /// <summary>저작본의 <c>skins</c> 자리 — 비면 쓰지 않는다. 코드는 <see cref="Skins"/>를 쓴다.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("skins")]
    public List<SkinAsset>? SkinsOrNull
    {
        get => Skins.Count == 0 ? null : Skins;
        set => Skins = value ?? new List<SkinAsset>();
    }

    public SkinAsset? FindSkin(string? name)
        => name is null ? null : Skins.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// 툴에서 정의한 세트들 — 세트의 뼈대.
    /// 저작본에는 <see cref="SetsOrNull"/>로 실린다(없으면 안 적는다). 컷 파일에는 컷이 품은 것이 오브젝트로 펼쳐져 나간다.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<SetAsset> Sets { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("sets")]
    public List<SetAsset>? SetsOrNull
    {
        get => Sets.Count == 0 ? null : Sets;
        set => Sets = value ?? new List<SetAsset>();
    }

    public SetAsset? FindSetOrCut(string? kind)
        => kind is null ? null : Sets.FirstOrDefault(r => string.Equals(r.Name, kind, StringComparison.Ordinal)) ?? FindCut(kind)?.AsSet; // 컷의 오브젝트 계층도 같은 이름으로 찾는다

    /// <summary>
    /// 컷들(<c>PLAN-standalone.md</c> §6) — 씬 + 카메라 + 타임라인. 저작본에는 <see cref="CutsOrNull"/>로 실린다(없으면 안 적는다).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<CutAsset> Cuts { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("cuts")]
    public List<CutAsset>? CutsOrNull
    {
        get => Cuts.Count == 0 ? null : Cuts;
        set => Cuts = value ?? new List<CutAsset>();
    }

    public CutAsset? FindCut(string? name)
        => name is null ? null : Cuts.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    /// <summary>세트와 컷의 오브젝트 계층 전부 — 품은 곳을 찾거나 세울 때.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<SetAsset> AllSetsAndCuts => Sets.Concat(Cuts.Select(c => c.AsSet));

    public TimelineAsset? Find(string name)
        => Timelines.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));
}

/// <summary>
/// 애니메이션 하나 — 컷의 타임라인이거나 세트의 애니메이션. **키는 "그 프레임의 모습"이다**: 키 값이 곧 그 속성의 값이고,
/// 키가 없는 속성은 오브젝트의 가만히 선 값 그대로다. 첫 키 앞과 마지막 키 뒤로는 그 키의 값이 이어진다.
/// </summary>
public sealed class TimelineAsset
{
    public string Name { get; set; } = "";

    /// <summary>이 타임라인이 얹히는 계층의 이름(setName) — 세트나 컷.</summary>
    public string Set { get; set; } = "";

    /// <summary>초.</summary>
    public float Duration { get; set; }

    public List<ChannelAsset> Channels { get; set; } = new();

    /// <summary>
    /// 이벤트 키 — 재생이 그 시각을 지나는 순간 울린다 — 컷 파일의 이벤트로 나간다("이 프레임에 소리").
    /// 시각 오름차순을 편집기가 지킨다. 저작본에는 <see cref="EventsOrNull"/>로 실린다(없으면 안 적는다).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<EventKeyAsset> Events { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("events")]
    public List<EventKeyAsset>? EventsOrNull
    {
        get => Events.Count == 0 ? null : Events;
        set => Events = value ?? new List<EventKeyAsset>();
    }

    /// <summary>
    /// 재생 블록(<c>PLAN-set.md</c> S10) — "품은 세트의 애니메이션을 언제 튼다". 세트의 애니메이션이 제 키와 함께 든다(세트 안의 작은 시간표).
    /// 시작 시각 오름차순. 저작본에는 <see cref="PlaysOrNull"/>로 실린다(없으면 안 적는다).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<PlayBlockAsset> Plays { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("plays")]
    public List<PlayBlockAsset>? PlaysOrNull
    {
        get => Plays.Count == 0 ? null : Plays;
        set => Plays = value ?? new List<PlayBlockAsset>();
    }

    /// <summary>
    /// 그림 키(<c>PLAN-standalone.md</c> C4) — "이 프레임부터 이 오브젝트는 이 그림이다". 사이값이 없는 **계단 키**다(표정 바꾸기).
    /// 시각 오름차순. 저작본에는 <see cref="ImageKeysOrNull"/>로 실린다(없으면 안 적는다).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<ImageKeyAsset> ImageKeys { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("imageKeys")]
    public List<ImageKeyAsset>? ImageKeysOrNull
    {
        get => ImageKeys.Count == 0 ? null : ImageKeys;
        set => ImageKeys = value ?? new List<ImageKeyAsset>();
    }

    /// <summary>
    /// 재생 블록을 다 펼쳐 구운 것(<see cref="Preview.TimelineFlatten"/>) — 품은 세트의 애니메이션이 경로 이름의 채널(<c>차.Body</c>)로 들어와 한 덩어리가 된 타임라인.
    /// 재생 블록이 없으면 null. 미리보기와 컷 파일은 이것을 쓴다 — 저작본에는 안 적힌다. 편집기가 문서가 바뀔 때마다 다시 굽는다.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TimelineAsset? Flattened { get; set; }

    public ChannelAsset? FindChannel(string node, ChannelKind kind)
        => Channels.FirstOrDefault(c => string.Equals(c.Object, node, StringComparison.Ordinal) && c.Kind == kind);
}

/// <summary>재생 블록 하나 — 품은 세트의 인스턴스(이 세트의 오브젝트 이름) · 그 세트의 애니메이션 · 시작 시각 · 되풀이 횟수.</summary>
public sealed class PlayBlockAsset
{
    /// <summary>인스턴스 오브젝트의 이름 — 다른 세트를 품은 오브젝트(<see cref="ObjectAsset.Set"/>).</summary>
    public string Object { get; set; } = "";

    /// <summary>품은 세트의 애니메이션 이름.</summary>
    public string Timeline { get; set; } = "";

    /// <summary>시작 시각(초, 이 애니메이션 기준).</summary>
    public float At { get; set; }

    /// <summary>되풀이 횟수(1 이상) — 같은 블록을 끝에 이어 그만큼 놓은 것.</summary>
    public int Repeat { get; set; } = 1;
}

/// <summary>그림 키 하나 — 그 시각(초)부터 그 오브젝트가 그 그림(문서 기준 경로)을 그린다. 다음 그림 키까지 이어진다.</summary>
public sealed class ImageKeyAsset
{
    public float T { get; set; }

    public string Object { get; set; } = "";

    public string Image { get; set; } = "";
}

/// <summary>이벤트 키 하나 — 시각(초) · 이름(<c>[a-z0-9_]+</c>) · 딸린 값(자유 글, 없어도 된다).</summary>
public sealed class EventKeyAsset
{
    public float T { get; set; }

    public string Name { get; set; } = "";

    public string? Value { get; set; }
}

/// <summary>노드 하나의 속성 하나. 성분이 null이면 "건드리지 않는다" — 그 성분은 가만히 선 값 그대로다.</summary>
public sealed class ChannelAsset
{
    public string Object { get; set; } = "";
    public ChannelKind Kind { get; set; }

    public ComponentCurve? X { get; set; }
    public ComponentCurve? Y { get; set; }
    public ComponentCurve? Z { get; set; }

    public ComponentCurve? Get(Axis axis) => axis switch
    {
        Axis.X => X,
        Axis.Y => Y,
        Axis.Z => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "모르는 축"),
    };

    public void Set(Axis axis, ComponentCurve? curve)
    {
        switch (axis)
        {
            case Axis.X: X = curve; break;
            case Axis.Y: Y = curve; break;
            case Axis.Z: Z = curve; break;
            default: throw new ArgumentOutOfRangeException(nameof(axis), axis, "모르는 축");
        }
    }
}

public enum Axis
{
    X,
    Y,
    Z,
}

/// <summary>성분 하나의 키 목록. 시각 오름차순을 편집기가 지킨다.</summary>
public sealed class ComponentCurve
{
    public List<KeyAsset> Keys { get; set; } = new();
}

/// <summary>
/// 키 하나. 수치 넷은 코어 <see cref="CurveKey"/>와 1:1이고, <see cref="Tangent"/>·<see cref="Broken"/>은
/// 그래프 편집기의 손잡이 상태다 — 컷 파일에는 탄젠트 **값**만 나간다.
/// </summary>
public sealed class KeyAsset
{
    /// <summary>초.</summary>
    public float T { get; set; }

    public float V { get; set; }
    public float InTangent { get; set; }
    public float OutTangent { get; set; }

    /// <summary>탄젠트를 누가 정하는가. <see cref="TangentMode.Free"/>는 적힌 값을 그대로 지킨다.</summary>
    public TangentMode Tangent { get; set; } = TangentMode.Free;

    /// <summary>in·out 탄젠트가 따로 노는가(마야의 break tangents).</summary>
    public bool Broken { get; set; }

    public CurveKey ToCurveKey() => new(T, V, InTangent, OutTangent);
}

/// <summary>
/// 마야의 탄젠트 종류 중 이 툴의 커브로 가능한 것(가중·계단은 없다 — 계단은 그림 키가 맡는다). 산수는 <c>TangentMath</c>.
/// 이름이 곧 저작 원본의 글자(소문자)다 — 바꾸면 옛 파일을 못 읽는다.
/// </summary>
public enum TangentMode
{
    /// <summary>이웃 할선, 극값·끝은 평평, 가파른 쪽의 3배 안으로(마야 auto에 가깝다).</summary>
    Auto,

    Flat,

    /// <summary>앞·뒤 키와의 할선(끊김).</summary>
    Linear,

    /// <summary>손으로 둔 값 그대로.</summary>
    Free,

    /// <summary>이웃을 잇는 할선 그대로(Catmull-Rom). 부드럽지만 넘칠 수 있다 — 마야 spline.</summary>
    Spline,

    /// <summary>auto보다 더 눌러 이웃 값을 넘지 않게. 이웃과 같은 값이면 평평 — 마야 plateau.</summary>
    Plateau,
}
