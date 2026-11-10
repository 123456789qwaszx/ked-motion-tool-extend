using Ked.Presentation.Core;
using Mt.Authoring.Model;
using Mt.Authoring.Rig;

namespace Mt.Authoring.Preview;

/// <summary>
/// 컷을 무대에 세운다(<c>PLAN-standalone.md</c> §6 · C2b) — 런타임이 실제 카메라와 깊이 없이 UI로 카메라를 흉내 내는 그 계산을 컷의 오브젝트에 준다.
///
/// 맨 위 오브젝트마다 **깊이**(depthLevel, 수)가 있고, 깊이가 두 가지를 정한다:
/// <list type="bullet">
/// <item><b>크기</b> — 런타임의 깊이 커브(<see cref="DepthTuningFile"/>). mid(14)에서 지은 그대로, 멀수록 작게. 오브젝트의 제 원점을 중심으로 줄어든다(자리는 그대로다).</item>
/// <item><b>카메라에 따르는 정도</b> — 밀면(pan) 먼 것은 덜·가까운 것은 더 따라오고, 당기면(zoom) 먼 것은 덜·가까운 것은 더 커진다.
/// 런타임의 층별 상수(<see cref="StageNodes.ResponseOf"/>)를 깊이의 눈금(far 0 · back 10 · mid 14 · front 16 · close 20) 사이에서 이어 쓴다 — 수가 층 사이에 있어도 끊기지 않는다.</item>
/// </list>
/// 트리의 모양은 런타임의 무대 뼈대와 같은 차례다: 당기기(전체 배율) › 밀기(전체 이동) › 오브젝트별 틀(층의 FramingTransform·FramingScale 자리) › 오브젝트.
/// 이 계산은 컷 파일을 읽는 쪽과 같아야 한다 — C6에서 Core로 옮긴다.
/// </summary>
public static class CutStage
{
    private const string Zoom = "~zoom";
    private const string Pan = "~pan";
    private const string Frame = "~frame/";

    /// <summary>깊이의 눈금과 그 층의 이름 — 가까워지는 차례.</summary>
    private static readonly (float Level, string Layer)[] Marks =
    {
        (DepthLevelLabels.Far, SceneLayers.Far),
        (DepthLevelLabels.Back, SceneLayers.Back),
        (DepthLevelLabels.Mid, SceneLayers.Mid),
        (DepthLevelLabels.Front, SceneLayers.Front),
        (DepthLevelLabels.Close, SceneLayers.Close),
    };

    /// <summary>깊이를 안 준 오브젝트의 깊이 — mid.</summary>
    public const float DefaultDepth = DepthLevelLabels.Mid;

    /// <summary>
    /// 컷의 오브젝트들을 세운 트리 — 카메라와 깊이는 아직 안 들어갔다(틀이 전부 제자리). 포즈를 잡은 뒤 <see cref="Apply"/>로 넣는다.
    /// 노드 키는 <paramref name="prefix"/> + 오브젝트 이름(세트를 세울 때와 같다 — 타임라인의 키가 그대로 얹힌다). 컷이 덤프에 없으면 null.
    /// </summary>
    public static RectNodeTree? Build(RigSchemaFile rig, CutAsset cut, string prefix)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(cut);

        if (!rig.TryGetRig(cut.Name, out RigSchemaRigDto dto))
        {
            return null;
        }

        var tree = new RectNodeTree(new RectSpace(rig.Dto.capturedUnderParentSize.ToVec2(), Vec2.Half));
        tree.Add(prefix + Zoom, null, RectNodeState.StretchFull);
        tree.Add(prefix + Pan, prefix + Zoom, RectNodeState.StretchFull);

        foreach (RigSchemaNodeDto node in dto.nodes)
        {
            RectNodeState state = RigSchemaLoader.ToState(node, cut.Name);

            if (node.id == RigSchemaLoader.RootKey)
            {
                tree.Add(prefix + node.id, prefix + Pan, state);
            }
            else if (IsTop(node))
            {
                // 맨 위 오브젝트 — 제 틀 밑에 선다(틀이 카메라에 따르는 정도를 받는다).
                tree.Add(prefix + Frame + node.id, prefix + Pan, RectNodeState.StretchFull);
                tree.Add(prefix + node.id, prefix + Frame + node.id, state);
            }
            else
            {
                tree.Add(prefix + node.id, prefix + node.parent, state);
            }
        }

        return tree;
    }

    private static bool IsTop(RigSchemaNodeDto node) => string.IsNullOrEmpty(node.parent) || node.parent == RigSchemaLoader.RootKey;

    /// <summary>
    /// 카메라와 깊이를 넣는다 — <see cref="Build"/>로 세워 포즈를 잡은 트리에 **한 번**(맨 위 오브젝트의 배율에 깊이의 배율을 곱하므로 두 번 넣으면 안 된다).
    /// </summary>
    /// <param name="depthOf">그 시각의 깊이(키로 움직일 때) — 없으면 오브젝트에 적힌 값.</param>
    public static void Apply(RectNodeTree tree, CutAsset cut, string prefix, CameraSettings camera, DepthTuningFile? depth, Func<RigNodeAsset, float?>? depthOf = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(cut);
        ArgumentNullException.ThrowIfNull(camera);

        float scale = ShotIntentMath.EvaluateCameraScale(camera.Zoom);
        var pan = new Vec2(camera.PanX, camera.PanY);

        if (tree.Contains(prefix + Zoom))
        {
            RectNodeState zoom = tree.GetState(prefix + Zoom);
            tree.SetState(prefix + Zoom, zoom.WithLocalScale(new Vec3(scale, scale, zoom.LocalScale.Z)));
            tree.SetState(prefix + Pan, tree.GetState(prefix + Pan).WithAnchoredPosition(pan));
        }

        foreach (RigNodeAsset node in cut.Nodes.Where(n => n.Parent.Length == 0))
        {
            string key = prefix + node.Id;
            string frameKey = prefix + Frame + node.Id;

            if (!tree.Contains(key) || !tree.Contains(frameKey))
            {
                continue;
            }

            float level = depthOf?.Invoke(node) ?? node.Depth ?? DefaultDepth;
            (Vec2 panResponse, float zoomResponse) = ResponseAt(level);
            float framing = MathF.Pow(scale, zoomResponse);
            RectNodeState frame = tree.GetState(frameKey);
            tree.SetState(frameKey, frame
                .WithAnchoredPosition(new Vec2(pan.X * panResponse.X, pan.Y * panResponse.Y))
                .WithLocalScale(new Vec3(framing, framing, frame.LocalScale.Z)));

            float size = SizeAt(level, depth);

            if (size != 1f)
            {
                RectNodeState state = tree.GetState(key);
                tree.SetState(key, state.WithLocalScale(new Vec3(state.LocalScale.X * size, state.LocalScale.Y * size, state.LocalScale.Z)));
            }
        }
    }

    /// <summary>깊이에 따른 크기 — mid에서 1(지은 그대로), 멀수록 작게. 깊이 커브가 없으면 1.</summary>
    public static float SizeAt(float level, DepthTuningFile? depth)
        => depth is not null && depth.TryScaleOf(level, out float at) && depth.TryScaleOf(DefaultDepth, out float mid) && mid > 1e-6f ? at / mid : 1f;

    /// <summary>
    /// 깊이에 따라 카메라에 따르는 정도 — 눈금 사이는 곧게 잇고, 양 끝 밖은 끝의 값(far보다 멀거나 close보다 가까워도 더 달라지지 않는다).
    /// </summary>
    public static (Vec2 PanResponse, float ZoomScaleResponse) ResponseAt(float level)
    {
        if (level <= Marks[0].Level)
        {
            return StageNodes.ResponseOf(Marks[0].Layer);
        }

        for (int i = 1; i < Marks.Length; i++)
        {
            if (level <= Marks[i].Level)
            {
                float t = (level - Marks[i - 1].Level) / (Marks[i].Level - Marks[i - 1].Level);
                (Vec2 panA, float zoomA) = StageNodes.ResponseOf(Marks[i - 1].Layer);
                (Vec2 panB, float zoomB) = StageNodes.ResponseOf(Marks[i].Layer);
                return (new Vec2(panA.X + (panB.X - panA.X) * t, panA.Y + (panB.Y - panA.Y) * t), zoomA + (zoomB - zoomA) * t);
            }
        }

        return StageNodes.ResponseOf(Marks[^1].Layer);
    }

    /// <summary>
    /// 그리는 순서 — 먼 것부터(깊이가 작은 맨 위 오브젝트와 그 밑의 것들이 먼저), 같은 깊이는 목록 순. 펼친 리그의 노드들을 그 차례로 다시 세운다.
    /// </summary>
    public static IReadOnlyList<RigSchemaNodeDto> DrawOrder(CutAsset cut, IReadOnlyList<RigSchemaNodeDto> nodes)
    {
        ArgumentNullException.ThrowIfNull(cut);
        ArgumentNullException.ThrowIfNull(nodes);
        var top = new Dictionary<string, string>(StringComparer.Ordinal); // 오브젝트 → 그 맨 위 조상

        foreach (RigNodeAsset node in cut.Nodes)
        {
            top[node.Id] = node.Parent.Length == 0 ? node.Id : top.GetValueOrDefault(node.Parent, node.Parent);
        }

        float DepthOf(RigSchemaNodeDto node)
        {
            string owner = RigAssetCodec.OwnerOf(node.id); // 품은 세트 안쪽의 것(a.b)은 그것을 품은 오브젝트를 따른다
            return top.TryGetValue(owner, out string? root) ? cut.Rig.Find(root)?.Depth ?? DefaultDepth : float.NegativeInfinity;
        }

        return nodes.Select((node, index) => (node, index)).OrderBy(e => DepthOf(e.node)).ThenBy(e => e.index).Select(e => e.node).ToList();
    }
}
