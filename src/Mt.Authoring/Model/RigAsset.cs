using Ked.Presentation.Core;

namespace Mt.Authoring.Model;

/// <summary>
/// 툴에서 정의한 리그(<c>PLAN-parts.md</c> §9) — 세트의 뼈대. Spine처럼 **툴이 원본**이다.
/// 컷의 오브젝트 계층도 같은 그릇이다(<see cref="CutAsset.Rig"/>).
///
/// 노드는 단순하다: 부모의 가운데에 앵커·피벗을 둔 사각형(위치 · 크기) + 그림을 그리는가 + 불투명도를 받는가.
/// 루트(<c>__root</c>)는 적지 않는다 — 내보낼 때 붙는다(부모를 꽉 채운다). 부모가 빈 노드는 루트 밑이다.
/// </summary>
public sealed class RigAsset
{
    /// <summary>rigKind — 모션·스킨이 이 이름으로 가리킨다. 영문자로 시작하는 영문·숫자·_.</summary>
    public string Kind { get; set; } = "";

    /// <summary>부모가 자식보다 먼저 온다. 그리는 순서도 이 순서다(뒤의 것이 위).</summary>
    public List<RigNodeAsset> Nodes { get; set; } = new();

    public RigNodeAsset? Find(string id) => Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal));
}

public sealed class RigNodeAsset
{
    /// <summary>새 그림 노드의 한 변(px) — 그림의 크기를 못 읽었을 때의 값이기도 하다.</summary>
    public const float DefaultImageSize = 160f;

    public string Id { get; set; } = "";

    /// <summary>부모 노드의 id. 비면 루트 밑.</summary>
    public string Parent { get; set; } = "";

    /// <summary>부모 가운데에서의 위치(px).</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>사각형의 크기(px). 그림 노드면 그림이 이 안에 종횡비로 맞춰진다. 0이면 점(묶는 노드).</summary>
    public float Width { get; set; }

    public float Height { get; set; }

    /// <summary>가만히 선 자세의 회전(도, 반시계 +)과 배율 — 배치 모드에서 잡는다(<c>PLAN-set.md</c> S2). 애니메이션은 이 위에 얹힌다.</summary>
    public float Rotation { get; set; }

    public float ScaleX { get; set; } = 1f;

    public float ScaleY { get; set; } = 1f;

    /// <summary>
    /// 색 — 있으면 **단색 사각형**이다: 그림 없이 이 노드의 사각형을 이 색으로 채운다(<c>PLAN-set.md</c> S4 — 화면 전환·소개의 패널).
    /// "#RRGGBB" 또는 "#RRGGBBAA". null이면 색 오브젝트가 아니다. 그림과 함께 켜면 그림 뒤에 색이 깔린다.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 품은 세트(<c>PLAN-set.md</c> S9) — 있으면 이 오브젝트는 **다른 세트의 인스턴스**다: 그 세트의 오브젝트들이 이 밑에 선다(참조 — 그 세트를 고치면 여기도 바뀐다).
    /// 이 오브젝트의 자리·배율·회전·불투명도는 품은 세트를 통째로 옮긴다. 세운 리그에서 품은 것의 이름은 <c>이 이름.그 이름</c>이다(<see cref="RigAssetCodec.PathSeparator"/>).
    /// </summary>
    public string? Set { get; set; }

    /// <summary>품은 세트에 입힐 스킨 — null이면 그 세트의 첫 스킨.</summary>
    public string? SetSkin { get; set; }

    /// <summary>
    /// 깊이 — 런타임의 <c>depthLevel</c>(수: far 0 · back 10 · mid 14 · front 16 · close 20은 그 눈금의 이름이다). **컷의 맨 위 오브젝트**만 뜻이 있다:
    /// 무대의 어느 깊이에 서는가(멀수록 작게 보이고 카메라에 덜 따라 움직인다). null이면 mid. 밑의 오브젝트는 부모를 따른다.
    /// </summary>
    public float? Depth { get; set; }

    /// <summary>그림을 그리는 노드(조각)인가 — 스킨이 여기에 그림을 준다.</summary>
    public bool Image { get; set; }

    /// <summary>불투명도를 받는가(CanvasGroup) — 알파 채널을 놓을 수 있다.</summary>
    public bool Alpha { get; set; }

    public RigNodeAsset Clone() => (RigNodeAsset)MemberwiseClone();
}

/// <summary>툴의 리그 ↔ 코어의 리그 스키마. 해석(트리 세우기)은 코어가 한다.</summary>
public static class RigAssetCodec
{
    public const string RootId = HierarchyLoader.RootKey;

    /// <summary>품은 세트의 오브젝트를 가리키는 경로의 구분 기호 — <c>차.Body</c>. 오브젝트 이름에는 못 쓰는 글자다(이름은 영문·숫자·_).</summary>
    public const char PathSeparator = '.';

    /// <summary>품는 깊이의 상한 — 순환은 편집기가 막지만, 손으로 쓴 문서가 끝없이 돌지 않게.</summary>
    public const int MaxNesting = 8;

    /// <summary>경로의 맨 앞 — 그 세트가 직접 가진 오브젝트의 이름(품은 것 안쪽의 이름이면 그것을 품은 오브젝트).</summary>
    public static string OwnerOf(string nodeId)
    {
        int at = nodeId.IndexOf(PathSeparator, StringComparison.Ordinal);
        return at < 0 ? nodeId : nodeId[..at];
    }

    /// <summary>
    /// 코어 스키마로 — 루트를 붙이고, 노드는 가운데 앵커·가운데 피벗으로. <paramref name="resolve"/>가 있으면 **품은 세트를 펼친다**:
    /// 인스턴스 오브젝트 밑에 그 세트의 오브젝트들이 <c>인스턴스.이름</c>으로 선다(되풀이해서 — 품은 것이 또 품었으면 <c>a.b.c</c>). 없는 세트·순환·너무 깊은 것은 건너뛴다.
    /// 그래서 세운 리그는 늘 한 덩어리다 — 미리보기도 컷 파일도 펼친 것을 본다(<c>PLAN-set.md</c> §7의 A).
    /// </summary>
    public static HierarchyDto ToDto(RigAsset rig, Vec2 stageSize, Func<string, RigAsset?>? resolve = null)
    {
        ArgumentNullException.ThrowIfNull(rig);
        var nodes = new List<HierarchyNodeDto>
        {
            new()
            {
                id = RootId,
                parent = "",
                anchoredPosition = F2(0f, 0f),
                anchorMin = F2(0f, 0f),
                anchorMax = F2(1f, 1f),
                pivot = F2(0.5f, 0.5f),
                sizeDelta = F2(0f, 0f),
                localScale = F3(1f, 1f, 1f),
                localEulerAngles = F3(0f, 0f, 0f),
            },
        };

        Expand(nodes, rig, "", RootId, resolve, new HashSet<string>(StringComparer.Ordinal) { rig.Kind }, 0);

        return new HierarchyDto { name = rig.Kind, nodes = nodes };
    }

    /// <summary>
    /// 크기 없는 점 노드를 맨 위에 더한다 — 그리지 않고 값만 담는 자리(컷의 카메라: 그 자리가 곧 밀기·당기기의 값이다). 키를 놓을 수 있는 오브젝트로 선다.
    /// </summary>
    public static HierarchyDto WithPoints(HierarchyDto dto, params string[] ids)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(ids);

        foreach (string id in ids)
        {
            dto.nodes.Add(new HierarchyNodeDto
            {
                id = id,
                parent = RootId,
                anchoredPosition = F2(0f, 0f),
                anchorMin = F2(0.5f, 0.5f),
                anchorMax = F2(0.5f, 0.5f),
                pivot = F2(0.5f, 0.5f),
                sizeDelta = F2(0f, 0f),
                localScale = F3(1f, 1f, 1f),
                localEulerAngles = F3(0f, 0f, 0f),
            });
        }

        return dto;
    }

    /// <summary>세트의 오브젝트들을 <paramref name="prefix"/>를 붙여 더한다 — 맨 위의 것은 <paramref name="under"/> 밑에. 인스턴스를 만나면 그 세트를 그 밑에 펼친다.</summary>
    private static void Expand(List<HierarchyNodeDto> nodes, RigAsset rig, string prefix, string under, Func<string, RigAsset?>? resolve, HashSet<string> open, int depth)
    {
        foreach (RigNodeAsset node in rig.Nodes)
        {
            string id = prefix + node.Id;
            nodes.Add(new HierarchyNodeDto
            {
                id = id,
                parent = string.IsNullOrEmpty(node.Parent) ? under : prefix + node.Parent,
                anchoredPosition = F2(node.X, node.Y),
                anchorMin = F2(0.5f, 0.5f),
                anchorMax = F2(0.5f, 0.5f),
                pivot = F2(0.5f, 0.5f),
                sizeDelta = F2(node.Width, node.Height),
                localScale = F3(node.ScaleX, node.ScaleY, 1f),
                localEulerAngles = F3(0f, 0f, node.Rotation),
                hasAlpha = node.Alpha,
                hasImage = node.Image,
                fillColor = node.Color,
            });

            // 품은 세트 — 이 오브젝트 밑에 펼친다. 제 조상을 다시 품는 것(순환)과 너무 깊은 것은 건너뛴다.
            if (node.Set is { Length: > 0 } inner && resolve?.Invoke(inner) is { } child && depth < MaxNesting && open.Add(inner))
            {
                Expand(nodes, child, id + PathSeparator, id, resolve, open, depth + 1);
                open.Remove(inner);
            }
        }
    }

    private static Float2Dto F2(float x, float y) => new() { x = x, y = y };

    private static Float3Dto F3(float x, float y, float z) => new() { x = x, y = y, z = z };
}
