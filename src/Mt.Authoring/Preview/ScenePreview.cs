using Ked.Presentation.Core;
using Mt.Authoring.Model;
using Mt.Authoring.Rig;

namespace Mt.Authoring.Preview;

/// <summary>
/// 씬 → 트리. 오브젝트마다 리그를 슬롯 접두사로 세우고(코어 <c>RigSchemaLoader.AddRigTo</c> — 리듀서의 slot 폴드와 같은 함수),
/// 배치 값을 배치 축 노드에 넣는다. 모션은 <see cref="Pose"/>가 고른 오브젝트 하나에 얹는다.
/// </summary>
public static class ScenePreview
{
    public const string DepthYNode = SetDepthStageReduction.DepthYNodeId;
    public const string DepthScaleNode = SetDepthStageReduction.DepthScaleNodeId;

    /// <summary>
    /// 씬 전체의 시작 상태. 덤프에 없는 rigKind의 오브젝트는 건너뛴다(<paramref name="missing"/>에 남긴다).
    /// 루트 공간은 덤프가 캡처된 공간이다(스트레치 노드 크기의 기준).
    /// </summary>
    /// <param name="depth">presets/depth.json. 있으면 레이어 라벨을 깊이(DepthScale)로 — 깊이 커브의 배율.</param>
    /// <param name="stage">
    /// 무대 뼈대(<c>PresentataionUIRoot</c>, rigKind "stage"). 있으면 <c>stage/</c> 접두사로 먼저 서고 오브젝트는
    /// <c>Stage{NN}Depth_{Layer}_Content</c> 밑에 붙으며, 카메라(zoom·pan)와 층 패럴랙스가 오브젝트가 다 선 뒤 뼈대 노드에 들어간다. 없으면 루트 직속(좌표는 같다 — 뼈대는 항등).
    /// </param>
    public static RectNodeTree Build(RigSchemaFile rig, SceneAsset scene, List<SceneObject>? missing = null, DepthTuningFile? depth = null, RigSchemaRigDto? stage = null)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(scene);

        var space = new RectSpace(rig.Dto.capturedUnderParentSize.ToVec2(), Vec2.Half);
        var state = new StageState(space);

        if (stage is not null)
        {
            RigSchemaLoader.AddRigTo(state.Nodes, stage, StageNodes.Prefix);
        }

        foreach (SceneObject o in scene.DrawOrder())
        {
            // 오브젝트는 그 무대·층의 Content에 선다. 런타임 GetDepthContent(stage, layer)와 같은 자리. 뼈대가 없거나 모르는 라벨이면 루트 직속.
            string? attach = null;

            if (stage is not null)
            {
                string content = StageNodes.KeyOf(StageNodes.ContentOf(o.Stage, o.Layer));
                attach = state.Nodes.Contains(content) ? content : null;
            }

            if (!rig.TryGetRig(o.Rig, out RigSchemaRigDto dto))
            {
                missing?.Add(o);
                continue;
            }

            RigSchemaLoader.AddRigTo(state.Nodes, dto, o.Prefix, attach!);
            state.RegisterSlot(o.Id);

            // 순서는 런타임 대본과 같이 slot → size(깊이) → 오프셋(move_by·rotate_by·scale_by).
            ApplyDepth(state, o, depth);
            ApplyPlacement(state.Nodes, o);
        }

        // 카메라(샷)는 오브젝트가 다 선 뒤에 — 런타임에서도 shot이 무대째 움직인다.
        if (stage is not null)
        {
            StageShotResponse.Apply(state.Nodes, scene.Camera);
        }

        return state.Nodes;
    }

    /// <summary>
    /// 깊이 폴드가 설 수 있는 리그인가 — 코어의 그 폴드는 캐릭터 리그의 축(깊이 노드와 측정 노드까지의 사슬)을 전제하고, 없으면 던진다.
    /// 세트 같은 다른 리그는 깊이 없이 선다(층은 그리는 순서만).
    /// </summary>
    private static bool HasDepthAxes(StageState state, SceneObject o)
        => state.Nodes.Contains(o.KeyOf(DepthYNode)) && state.Nodes.Contains(o.KeyOf(DepthScaleNode)) && state.Nodes.Contains(o.KeyOf(SetDepthStageReduction.MeasureNodeId));

    /// <summary>배치 값을 배치 축 노드에. 그 노드가 없는 리그(배경 등)면 루트 노드에 — 그래서 어느 오브젝트든 옮길 수 있다.</summary>
    public static void ApplyPlacement(RectNodeTree tree, SceneObject o)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(o);

        string root = RootKeyOf(o);
        string position = tree.Contains(o.KeyOf(PlacementNodes.Position)) ? o.KeyOf(PlacementNodes.Position) : root;
        string rotation = tree.Contains(o.KeyOf(PlacementNodes.Rotation)) ? o.KeyOf(PlacementNodes.Rotation) : root;
        string scale = tree.Contains(o.KeyOf(PlacementNodes.Scale)) ? o.KeyOf(PlacementNodes.Scale) : root;

        if (tree.Contains(position))
        {
            tree.SetState(position, tree.GetState(position).WithAnchoredPosition(new Vec2(o.PositionX, o.PositionY)));
        }

        if (tree.Contains(rotation))
        {
            RectNodeState state = tree.GetState(rotation);
            tree.SetState(rotation, state.WithLocalEuler(new Vec3(state.LocalEulerAngles.X, state.LocalEulerAngles.Y, o.RotationZ)));
        }

        if (tree.Contains(scale))
        {
            RectNodeState state = tree.GetState(scale);
            tree.SetState(scale, state.WithLocalScale(new Vec3(o.ScaleX, o.ScaleY, state.LocalScale.Z)));
        }
    }

    /// <summary>
    /// 레이어 → 깊이(DepthScale 배율). 런타임 size 폴드와 **같은 함수**(<see cref="SetDepthStageReduction"/>)를 지나되 기준점 표 없이 —
    /// 깊이 커브의 배율만 들어가고 오브젝트는 제 원점을 중심으로 줄어든다(컷의 깊이와 같다). 커브가 없거나 리그에 깊이 축이 없으면 건너뛴다(false).
    /// </summary>
    public static bool ApplyDepth(StageState state, SceneObject o, DepthTuningFile? depth)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(o);

        if (depth is null || !SceneLayers.IsKnown(o.Layer) || !HasDepthAxes(state, o))
        {
            return false;
        }

        if (!SetDepthStageReduction.TryReduce(state, o.Id, "", o.Layer, "bust", depth.Level, null!, out StageNodeClaim depthY, out StageNodeClaim depthScale, out _))
        {
            return false;
        }

        state.Apply(depthY);
        state.Apply(depthScale);
        return true;
    }

    /// <summary>모션을 오브젝트 하나에 얹은 시각 t의 포즈. 채널의 노드가 그 리그에 없으면 그 채널은 건너뛴다.</summary>
    public static RectNodeTree Pose(RectNodeTree template, SceneObject target, MotionAsset motion, double seconds, Func<Graph.CurveRef, bool>? isMuted = null, IDictionary<string, float>? alphas = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(motion);
        return MotionPreview.Pose(template, motion, seconds, motion.PreviewMode, isMuted, target.Prefix, alphas);
    }

    /// <summary>
    /// 이 오브젝트에 모션을 입혀 볼 수 있나 — rigKind가 같거나, 채널의 노드가 전부 그 리그에 있으면(공통 스키마면 공유) 된다.
    /// </summary>
    public static bool CanWear(RigSchemaFile rig, SceneObject o, MotionAsset motion)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(motion);

        if (string.Equals(o.Rig, motion.Rig, StringComparison.Ordinal))
        {
            return true;
        }

        // 채널이 없으면 공유할 것도 없다 — 리그가 다르면 안 맞는 것으로 본다.
        return motion.Channels.Count > 0
            && rig.NodeIds.TryGetValue(o.Rig, out ICollection<string>? nodes)
            && motion.Channels.All(c => nodes.Contains(c.Node));
    }

    /// <summary>
    /// 그림도 색도 없는 오브젝트의 윤곽을 그릴 노드 — 덤프 리그의 그림 자리 노드가 있으면 그것(포즈를 따라 움직인다), 없으면 null(루트 rect를 윤곽으로 그린다).
    /// </summary>
    public static string? OutlineKeyOf(RectNodeTree tree, SceneObject o)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(o);
        string key = o.KeyOf(OutlineNode);
        return tree.Contains(key) ? key : null;
    }

    /// <summary>덤프 리그(캐릭터)에서 그림이 올라가던 노드 — 이 툴은 거기에 그림을 올리지 않고 그 rect를 윤곽으로만 그린다.</summary>
    public const string OutlineNode = "CharacterPortraitSprite_Image";

    public static string RootKeyOf(SceneObject o) => o.KeyOf(RigSchemaLoader.RootKey);
}
