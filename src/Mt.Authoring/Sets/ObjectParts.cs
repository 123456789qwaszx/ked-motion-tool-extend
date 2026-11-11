using Mt.Authoring.Model;

namespace Mt.Authoring.Sets;

/// <summary>
/// 세트의 조각 — 그림을 하나씩 그리는 오브젝트들과 색 사각형. 오브젝트의 "그림" 표시와 색에서 읽는다.
/// </summary>
public static class ObjectParts
{
    /// <summary>그 세트 · 컷의 색 오브젝트 — 오브젝트 id → 색("#RRGGBB[AA]"). 없으면 빈 표.</summary>
    public static IReadOnlyDictionary<string, string> ColorsOf(BuiltSets? built, string name)
        => built is not null && built.TryGetObjects(name, out IReadOnlyList<ExpandedObject> objects)
            ? objects.Where(o => o.Color is not null).ToDictionary(o => o.Id, o => o.Color!, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>그 세트 · 컷의 그림 오브젝트 id — 적힌 순서(= 그리는 순서: 뒤의 것이 위). 없는 이름이면 빈 목록.</summary>
    public static IReadOnlyList<string> Of(BuiltSets? built, string name)
        => built is not null && built.TryGetObjects(name, out IReadOnlyList<ExpandedObject> objects)
            ? objects.Where(o => o.Image).Select(o => o.Id).ToList()
            : Array.Empty<string>();
}
