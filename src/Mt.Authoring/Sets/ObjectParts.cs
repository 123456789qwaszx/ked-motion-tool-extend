using Ked.Presentation.Core;

namespace Mt.Authoring.Sets;

/// <summary>
/// 세트의 조각 — 그림을 하나씩 그리는 오브젝트들(<c>PLAN-parts.md</c> P1)과 색 사각형. 오브젝트의 "그림" 표시와 색에서 읽는다.
/// </summary>
public static class ObjectParts
{
    /// <summary>세트의 조각 노드 id — 적힌 순서(= 그리는 순서: 뒤의 것이 위).</summary>
    public static IReadOnlyList<string> Of(HierarchyDto set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.nodes.Where(n => n.hasImage).Select(n => n.id).ToList();
    }

    /// <summary>그 setName의 색 오브젝트 — 노드 id → 색("#RRGGBB[AA]"). 없으면 빈 표.</summary>
    public static IReadOnlyDictionary<string, string> ColorsOf(BuiltSets? file, string setName)
        => file is not null && file.TryGetSet(setName, out HierarchyDto dto)
            ? dto.nodes.Where(n => !string.IsNullOrEmpty(n.fillColor)).ToDictionary(n => n.id, n => n.fillColor, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>그 setName의 조각 노드 id. 없는 세트면 빈 목록.</summary>
    public static IReadOnlyList<string> Of(BuiltSets? file, string setName)
        => file is not null && file.TryGetSet(setName, out HierarchyDto dto) ? Of(dto) : Array.Empty<string>();
}
