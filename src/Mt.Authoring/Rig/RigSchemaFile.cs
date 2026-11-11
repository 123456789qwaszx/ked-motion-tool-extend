using Ked.Presentation.Core;

namespace Mt.Authoring.Rig;

/// <summary>
/// 문서가 정의한 것(세트 · 컷)을 세운 한 벌 — 뷰포트 · 검증 · 하이어아키가 읽는 모양. 파일이 아니라 문서에서 그때그때 세운다(<see cref="RigAssets.Compose"/>).
/// 노드 이름·계층·앵커·피벗·크기는 전부 여기서 온다.
///
/// DTO는 코어 <c>Tuning/</c>의 것 그대로이고, 트리로 세우는 해석(<see cref="RigSchemaLoader"/>)도 코어가 한다.
/// </summary>
public sealed class RigSchemaFile
{
    private readonly Dictionary<string, ICollection<string>> _nodeIds;
    private readonly Dictionary<string, ICollection<string>> _alphaNodeIds;

    private RigSchemaFile(RigSchemasFileDto dto)
    {
        Dto = dto;
        _nodeIds = new Dictionary<string, ICollection<string>>(StringComparer.Ordinal);
        _alphaNodeIds = new Dictionary<string, ICollection<string>>(StringComparer.Ordinal);

        foreach (RigSchemaRigDto rig in dto.rigs)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (RigSchemaNodeDto node in rig.nodes)
            {
                ids.Add(node.id);
            }

            _nodeIds[rig.rigKind] = ids;
            _alphaNodeIds[rig.rigKind] = new HashSet<string>(rig.nodes.Where(n => n.hasCanvasGroup).Select(n => n.id), StringComparer.Ordinal);
        }
    }

    public RigSchemasFileDto Dto { get; }

    public IEnumerable<string> RigKinds => _nodeIds.Keys;

    /// <summary>rigKind → 노드 id 집합. <c>MotionValidator</c>에 그대로 넘긴다.</summary>
    public IReadOnlyDictionary<string, ICollection<string>> NodeIds => _nodeIds;

    /// <summary>
    /// rigKind → 불투명도를 받을 수 있는 노드(오브젝트의 "불투명도 받기"). 알파 채널은 이 노드에만 놓인다.
    /// </summary>
    public IReadOnlyDictionary<string, ICollection<string>> AlphaNodeIds => _alphaNodeIds;

    public bool TryGetRig(string rigKind, out RigSchemaRigDto rig)
    {
        foreach (RigSchemaRigDto candidate in Dto.rigs)
        {
            if (string.Equals(candidate.rigKind, rigKind, StringComparison.Ordinal))
            {
                rig = candidate;
                return true;
            }
        }

        rig = null!;
        return false;
    }

    /// <summary>리그 하나를 코어 트리로 세운다 — 뷰포트와 미리보기의 시작값이 여기서 나온다.</summary>
    public RectNodeTree BuildTree(string rigKind, string keyPrefix = "")
        => RigSchemaLoader.BuildTree(Dto, rigKind, keyPrefix);

    /// <summary>리그들로 한 벌을 세운다 — 무대 크기는 <paramref name="stageSize"/>. 세울 수 없는 모양의 리그면 예외(트리를 한 번 세워 본다).</summary>
    public static RigSchemaFile Of(Vec2 stageSize, IEnumerable<RigSchemaRigDto> rigs)
    {
        ArgumentNullException.ThrowIfNull(rigs);
        var dto = new RigSchemasFileDto { capturedUnderParentSize = new Float2Dto { x = stageSize.X, y = stageSize.Y }, rigs = rigs.ToList() };

        foreach (RigSchemaRigDto rig in dto.rigs)
        {
            RigSchemaLoader.BuildTree(dto, rig.rigKind);
        }

        return new RigSchemaFile(dto);
    }

    /// <summary>리그가 하나도 없는 빈 한 벌 — 새 문서, 또는 문서의 리그를 세우지 못했을 때.</summary>
    public static RigSchemaFile Empty(Vec2 stageSize) => Of(stageSize, Array.Empty<RigSchemaRigDto>());
}
