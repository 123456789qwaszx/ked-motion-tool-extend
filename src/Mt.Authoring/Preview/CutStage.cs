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

    /// <summary>
    /// 카메라의 점 — 컷의 오브젝트 계층에 함께 서는 크기 없는 오브젝트 둘. 그 자리가 곧 카메라의 값이다:
    /// <see cref="CameraPan"/>의 x·y = 밀기(px), <see cref="CameraZoom"/>의 x = 당기기. 그래서 카메라가 **같은 타임라인의 트랙**이 된다 — 자리 채널에 키를 놓으면 카메라가 움직인다.
    /// 키가 없는 동안은 컷에 적힌 카메라 값(<see cref="CutAsset.Camera"/>)으로 선다.
    /// </summary>
    public const string CameraPan = CameraRig.PanNode;
    public const string CameraZoom = CameraRig.ZoomNode;

    /// <summary>카메라의 점인가 — 하이어아키의 오브젝트가 아니다(그리지도 집지도 않는다).</summary>
    public static bool IsCameraNode(string? nodeId) => nodeId is CameraPan or CameraZoom;

    /// <summary>
    /// 깊이의 점 — 맨 위 오브젝트마다 하나씩 함께 서는 크기 없는 오브젝트(<c>이름__depth</c>). 그 자리의 x가 곧 그 오브젝트의 깊이다:
    /// 카메라와 같은 길로 **타임라인의 트랙**이 된다(자리 채널의 x에 키를 놓으면 오브젝트가 멀어지고 가까워진다). 키가 없는 동안은 오브젝트에 적힌 깊이로 선다.
    /// </summary>
    public const string DepthSuffix = "__depth";

    public static string DepthNodeOf(string objectId) => objectId + DepthSuffix;

    public static bool IsDepthNode(string? nodeId) => nodeId is not null && nodeId.EndsWith(DepthSuffix, StringComparison.Ordinal);

    /// <summary>값만 담는 점(카메라 · 깊이)인가 — 오브젝트가 아니다.</summary>
    public static bool IsPointNode(string? nodeId) => IsCameraNode(nodeId) || IsDepthNode(nodeId);

    /// <summary>컷의 오브젝트 계층과 함께 서는 점들의 이름 — 카메라 둘 + 맨 위 오브젝트마다 깊이 하나.</summary>
    public static string[] PointsOf(CutAsset cut)
        => new[] { CameraPan, CameraZoom }.Concat(cut.Nodes.Where(n => n.Parent.Length == 0).Select(n => DepthNodeOf(n.Id))).ToArray();

    /// <summary>깊이를 안 준 오브젝트의 깊이 — mid.</summary>
    public const float DefaultDepth = CutMath.DefaultDepth;

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
            else if (node.id == CameraPan)
            {
                // 카메라의 점 — 가만히 선 자리가 컷의 카메라 값이다. 카메라에 따라 움직이지 않는 자리(당기기 틀의 바깥)에 둔다.
                tree.Add(prefix + node.id, null, state.WithAnchoredPosition(new Vec2(cut.Camera.PanX, cut.Camera.PanY)));
            }
            else if (node.id == CameraZoom)
            {
                tree.Add(prefix + node.id, null, state.WithAnchoredPosition(new Vec2(cut.Camera.Zoom, 0f)));
            }
            else if (IsDepthNode(node.id))
            {
                // 깊이의 점 — 가만히 선 자리가 그 오브젝트에 적힌 깊이다.
                float rest = cut.Rig.Find(node.id[..^DepthSuffix.Length])?.Depth ?? DefaultDepth;
                tree.Add(prefix + node.id, null, state.WithAnchoredPosition(new Vec2(rest, 0f)));
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
    /// <remarks>카메라와 깊이는 트리에 선 점에서 읽는다 — 타임라인의 키가 그 점을 움직였으면 그 값이다.</remarks>
    public static void Apply(RectNodeTree tree, CutAsset cut, string prefix, DepthTuningFile? depth)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(cut);
        CameraSettings camera = CameraOf(tree, cut, prefix);

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

            float level = DepthOf(tree, node, prefix);
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

    /// <summary>그 포즈의 카메라 — 카메라의 점의 자리. 점이 없는 트리면 컷에 적힌 값.</summary>
    public static CameraSettings CameraOf(RectNodeTree tree, CutAsset cut, string prefix)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(cut);

        if (!tree.Contains(prefix + CameraPan) || !tree.Contains(prefix + CameraZoom))
        {
            return cut.Camera;
        }

        Vec2 pan = tree.GetState(prefix + CameraPan).AnchoredPosition;
        return new CameraSettings { PanX = pan.X, PanY = pan.Y, Zoom = tree.GetState(prefix + CameraZoom).AnchoredPosition.X };
    }

    /// <summary>그 포즈에서 그 맨 위 오브젝트의 깊이 — 깊이의 점의 자리. 점이 없는 트리면 오브젝트에 적힌 값.</summary>
    public static float DepthOf(RectNodeTree? tree, RigNodeAsset node, string prefix)
    {
        ArgumentNullException.ThrowIfNull(node);
        string key = prefix + DepthNodeOf(node.Id);
        return tree is not null && tree.Contains(key) ? tree.GetState(key).AnchoredPosition.X : node.Depth ?? DefaultDepth;
    }

    /// <summary>깊이에 따른 크기 — mid에서 1(지은 그대로), 멀수록 작게. 깊이 커브가 없으면 1. 계산은 코어(<see cref="CutMath.SizeAt"/>) — 게임의 재생과 같은 함수다.</summary>
    public static float SizeAt(float level, DepthTuningFile? depth) => CutMath.SizeAt(level, depth?.Level!);

    /// <summary>
    /// 깊이에 따라 카메라에 따르는 정도 — 눈금 사이는 곧게 잇고, 양 끝 밖은 끝의 값. 계산은 코어(<see cref="CutMath.ResponseAt"/>).
    /// </summary>
    public static (Vec2 PanResponse, float ZoomScaleResponse) ResponseAt(float level)
    {
        CutMath.ResponseAt(level, out float pan, out float zoom);
        return (new Vec2(pan, 0f), zoom);
    }

    /// <summary>
    /// 그리는 순서 — 먼 것부터(깊이가 작은 맨 위 오브젝트와 그 밑의 것들이 먼저), 같은 깊이는 목록 순. 펼친 리그의 노드들을 그 차례로 다시 세운다.
    /// </summary>
    /// <param name="pose">그 시각의 포즈 — 주면 키로 움직인 깊이의 차례다. 없으면 오브젝트에 적힌 깊이.</param>
    public static IReadOnlyList<RigSchemaNodeDto> DrawOrder(CutAsset cut, IReadOnlyList<RigSchemaNodeDto> nodes, RectNodeTree? pose = null, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(cut);
        ArgumentNullException.ThrowIfNull(nodes);
        var top = new Dictionary<string, string>(StringComparer.Ordinal); // 오브젝트 → 그 맨 위 조상

        foreach (RigNodeAsset node in cut.Nodes)
        {
            top[node.Id] = node.Parent.Length == 0 ? node.Id : top.GetValueOrDefault(node.Parent, node.Parent);
        }

        float OrderDepth(RigSchemaNodeDto node)
        {
            string owner = RigAssetCodec.OwnerOf(node.id); // 품은 세트 안쪽의 것(a.b)은 그것을 품은 오브젝트를 따른다
            return top.TryGetValue(owner, out string? root) && cut.Rig.Find(root) is { } rootNode ? DepthOf(pose, rootNode, prefix) : float.NegativeInfinity;
        }

        return nodes.Select((node, index) => (node, index)).OrderBy(e => OrderDepth(e.node)).ThenBy(e => e.index).Select(e => e.node).ToList();
    }
}
