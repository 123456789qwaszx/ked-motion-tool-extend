using System.Text.Json.Serialization;

namespace Mt.Authoring.Model;

/// <summary>
/// 컷(<c>PLAN-standalone.md</c> §6) — 이 툴의 단위: **씬(계층 구조의 오브젝트들) + 카메라 + 정해진 길이의 타임라인**.
/// 캐릭터·배경·소품이 전부 오브젝트이고, 그 기간의 움직임을 키프레임으로 다 잡아 둔다. 컷마다 새로 시작한다(앞 컷의 끝을 이어받지 않는다).
///
/// 오브젝트 계층은 세트와 같은 것이다(<see cref="RigNodeAsset"/>) — 그래서 배치·키·품기·새 세트로 빼기가 그대로 컷에서 돈다.
/// 컷의 이름이 곧 그 계층의 이름(<see cref="RigAsset.Kind"/>)이고, 타임라인은 같은 이름의 애니메이션(<see cref="MotionAsset"/>) 하나다.
/// 맨 위 오브젝트는 깊이(<see cref="RigNodeAsset.Depth"/> — 런타임의 depthLevel)를 가진다.
/// </summary>
public sealed class CutAsset
{
    /// <summary>프레임 수의 기본 — 240프레임 = 10초.</summary>
    public const int DefaultFrames = 240;

    /// <summary>초당 프레임 — 컷의 시간은 프레임으로 센다.</summary>
    public const int Fps = 24;

    private string _name = "";
    private List<RigNodeAsset> _nodes = new();
    private RigAsset? _rig;

    /// <summary>이름 — 영문 소문자·숫자·_. 오브젝트 계층과 타임라인이 이 이름으로 묶인다.</summary>
    public string Name
    {
        get => _name;
        set
        {
            _name = value ?? "";

            if (_rig is not null)
            {
                _rig.Kind = _name;
            }
        }
    }

    /// <summary>길이(프레임).</summary>
    public int Frames { get; set; } = DefaultFrames;

    /// <summary>카메라의 가만히 선 값 — 키가 없는 동안의 밀기·당기기.</summary>
    public CameraSettings Camera { get; set; } = new();

    /// <summary>오브젝트들 — 부모가 자식보다 먼저 온다. 그리는 순서도 이 순서다(같은 깊이 안에서 뒤의 것이 위).</summary>
    [JsonPropertyName("objects")]
    public List<RigNodeAsset> Nodes
    {
        get => _nodes;
        set
        {
            _nodes = value ?? new List<RigNodeAsset>();

            if (_rig is not null)
            {
                _rig.Nodes = _nodes;
            }
        }
    }

    /// <summary>오브젝트 계층을 세트와 같은 모양으로 — 같은 목록을 본다(사본이 아니다). 세트를 고치는 명령이 그대로 컷을 고친다.</summary>
    [JsonIgnore]
    public RigAsset Rig => _rig ??= new RigAsset { Kind = _name, Nodes = _nodes };

    /// <summary>길이(초).</summary>
    [JsonIgnore]
    public float Seconds => Frames / (float)Fps;
}

/// <summary>카메라 — zoom 1당 배율 0.05(코어 <c>CutMath.CameraScale</c>), pan은 무대 px.</summary>
public sealed class CameraSettings
{
    public float Zoom { get; set; }

    public float PanX { get; set; }

    public float PanY { get; set; }
}
