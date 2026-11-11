using Ked.Presentation.Core;
using Mt.Authoring.Model;

namespace Mt.Authoring.Sets;

/// <summary>
/// 문서가 정의한 것(세트 · 컷)을 세운 한 벌 — 뷰포트 · 검증 · 하이어아키가 읽는 모양. 파일이 아니라 문서에서 그때그때 세운다(<see cref="SetBuilder.Compose"/>).
/// 이름마다 펼친 오브젝트들(<see cref="ExpandedObject"/>)을 들고, 트리는 <see cref="SetCodec.BuildTree"/>가 세운다.
/// </summary>
public sealed class BuiltSets
{
    private readonly Dictionary<string, IReadOnlyList<ExpandedObject>> _sets;
    private readonly Dictionary<string, ICollection<string>> _nodeIds;
    private readonly Dictionary<string, ICollection<string>> _alphaNodeIds;

    private BuiltSets(Vec2 stageSize, Dictionary<string, IReadOnlyList<ExpandedObject>> sets)
    {
        StageSize = stageSize;
        _sets = sets;
        _nodeIds = sets.ToDictionary(e => e.Key, e => (ICollection<string>)new HashSet<string>(e.Value.Select(o => o.Id), StringComparer.Ordinal), StringComparer.Ordinal);
        _alphaNodeIds = sets.ToDictionary(e => e.Key, e => (ICollection<string>)new HashSet<string>(e.Value.Where(o => o.Alpha).Select(o => o.Id), StringComparer.Ordinal), StringComparer.Ordinal);
    }

    /// <summary>무대(트리가 딛고 서는 공간)의 크기.</summary>
    public Vec2 StageSize { get; }

    public IEnumerable<string> SetNames => _sets.Keys;

    /// <summary>이름 → 오브젝트 id 집합. <c>DocumentValidator</c>에 그대로 넘긴다.</summary>
    public IReadOnlyDictionary<string, ICollection<string>> NodeIds => _nodeIds;

    /// <summary>이름 → 불투명도를 받을 수 있는 오브젝트(오브젝트의 "불투명도 받기"). 알파 채널은 이것에만 놓인다.</summary>
    public IReadOnlyDictionary<string, ICollection<string>> AlphaNodeIds => _alphaNodeIds;

    /// <summary>그 세트 · 컷의 펼친 오브젝트들 — 적힌 순서(부모 먼저).</summary>
    public bool TryGetObjects(string name, out IReadOnlyList<ExpandedObject> objects)
    {
        if (_sets.TryGetValue(name, out IReadOnlyList<ExpandedObject>? found))
        {
            objects = found;
            return true;
        }

        objects = Array.Empty<ExpandedObject>();
        return false;
    }

    /// <summary>세트 하나를 코어 트리로 세운다 — 뷰포트와 미리보기의 시작값이 여기서 나온다. 없는 이름이면 던진다.</summary>
    public RectNodeTree BuildTree(string name, string keyPrefix = "")
        => TryGetObjects(name, out IReadOnlyList<ExpandedObject> objects)
            ? SetCodec.BuildTree(objects, StageSize, keyPrefix)
            : throw new KeyNotFoundException($"'{name}'라는 세트나 컷이 없다. 있는 것: [{string.Join(", ", _sets.Keys)}]");

    /// <summary>이름과 펼친 오브젝트들로 한 벌을 세운다. 세울 수 없는 모양이면 예외(트리를 한 번 세워 본다).</summary>
    public static BuiltSets Of(Vec2 stageSize, IEnumerable<(string Name, List<ExpandedObject> Objects)> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);
        var map = new Dictionary<string, IReadOnlyList<ExpandedObject>>(StringComparer.Ordinal);

        foreach ((string name, List<ExpandedObject> objects) in sets)
        {
            SetCodec.BuildTree(objects, stageSize);
            map[name] = objects;
        }

        return new BuiltSets(stageSize, map);
    }

    /// <summary>세트가 하나도 없는 빈 한 벌 — 새 문서, 또는 문서의 세트를 세우지 못했을 때.</summary>
    public static BuiltSets Empty(Vec2 stageSize) => Of(stageSize, Array.Empty<(string, List<ExpandedObject>)>());
}
