using Ked.Presentation.Core;

namespace Mt.Authoring.Sets;

/// <summary>
/// 문서가 정의한 것(세트 · 컷)을 세운 한 벌 — 뷰포트 · 검증 · 하이어아키가 읽는 모양. 파일이 아니라 문서에서 그때그때 세운다(<see cref="SetBuilder.Compose"/>).
/// 노드 이름·계층·앵커·피벗·크기는 전부 여기서 온다.
///
/// DTO는 코어 <c>Tuning/</c>의 것 그대로이고, 트리로 세우는 해석(<see cref="HierarchyLoader"/>)도 코어가 한다.
/// </summary>
public sealed class BuiltSets
{
    private readonly Dictionary<string, ICollection<string>> _nodeIds;
    private readonly Dictionary<string, ICollection<string>> _alphaNodeIds;

    private BuiltSets(HierarchyFileDto dto)
    {
        Dto = dto;
        _nodeIds = new Dictionary<string, ICollection<string>>(StringComparer.Ordinal);
        _alphaNodeIds = new Dictionary<string, ICollection<string>>(StringComparer.Ordinal);

        foreach (HierarchyDto set in dto.hierarchies)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (HierarchyNodeDto node in set.nodes)
            {
                ids.Add(node.id);
            }

            _nodeIds[set.name] = ids;
            _alphaNodeIds[set.name] = new HashSet<string>(set.nodes.Where(n => n.hasAlpha).Select(n => n.id), StringComparer.Ordinal);
        }
    }

    public HierarchyFileDto Dto { get; }

    public IEnumerable<string> SetNames => _nodeIds.Keys;

    /// <summary>setName → 노드 id 집합. <c>DocumentValidator</c>에 그대로 넘긴다.</summary>
    public IReadOnlyDictionary<string, ICollection<string>> NodeIds => _nodeIds;

    /// <summary>
    /// setName → 불투명도를 받을 수 있는 노드(오브젝트의 "불투명도 받기"). 알파 채널은 이 노드에만 놓인다.
    /// </summary>
    public IReadOnlyDictionary<string, ICollection<string>> AlphaNodeIds => _alphaNodeIds;

    public bool TryGetSet(string setName, out HierarchyDto set)
    {
        foreach (HierarchyDto candidate in Dto.hierarchies)
        {
            if (string.Equals(candidate.name, setName, StringComparison.Ordinal))
            {
                set = candidate;
                return true;
            }
        }

        set = null!;
        return false;
    }

    /// <summary>세트 하나를 코어 트리로 세운다 — 뷰포트와 미리보기의 시작값이 여기서 나온다.</summary>
    public RectNodeTree BuildTree(string setName, string keyPrefix = "")
        => HierarchyLoader.BuildTree(Dto, setName, keyPrefix);

    /// <summary>세트들로 한 벌을 세운다 — 무대 크기는 <paramref name="stageSize"/>. 세울 수 없는 모양의 세트면 예외(트리를 한 번 세워 본다).</summary>
    public static BuiltSets Of(Vec2 stageSize, IEnumerable<HierarchyDto> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);
        var dto = new HierarchyFileDto { stageSize = new Float2Dto { x = stageSize.X, y = stageSize.Y }, hierarchies = sets.ToList() };

        foreach (HierarchyDto set in dto.hierarchies)
        {
            HierarchyLoader.BuildTree(dto, set.name);
        }

        return new BuiltSets(dto);
    }

    /// <summary>세트가 하나도 없는 빈 한 벌 — 새 문서, 또는 문서의 세트를 세우지 못했을 때.</summary>
    public static BuiltSets Empty(Vec2 stageSize) => Of(stageSize, Array.Empty<HierarchyDto>());
}
