using Ked.Presentation.Core;
using Mt.Authoring.Graph;
using Mt.Authoring.Model;

namespace Mt.Authoring.Preview;

/// <summary>
/// 시각 t의 포즈 — 가만히 선 트리(세트·컷을 세운 것)에 애니메이션의 키를 얹은 것.
///
/// **키는 "그 프레임의 모습"이다**: 키가 있는 성분은 커브의 값이 곧 그 속성의 값이고, 키가 없는 성분은 가만히 선 값 그대로다.
/// 첫 키 앞과 마지막 키 뒤로는 그 키의 값이 이어진다(길이를 지나도 마지막 모습이 남는다). 커브의 계산은 코어(<see cref="CurveFunctions.Evaluate"/>) —
/// 컷 파일을 읽는 재생(<see cref="CutPlayer"/>)과 같은 함수라 뷰포트가 보여 주는 포즈 = 게임이 재생하는 포즈다.
/// 재생 블록이 있는 애니메이션은 펼쳐 구운 것(<see cref="MotionAsset.Flattened"/>)으로 본다.
/// </summary>
public static class MotionPreview
{
    /// <param name="isMuted">뮤트된 성분(재생에서 뺀다 — 가만히 선 값 그대로). null이면 없음.</param>
    /// <param name="keyPrefix">노드 키의 접두사(예: "set/") — 접두사를 붙여 세운 트리에 얹을 때. 비면 접두사 없는 트리.</param>
    /// <param name="alphas">
    /// 노드 키 → 불투명도(없는 키는 1). 불투명도 채널은 트리가 아니라 이 표에 적힌다 — 불투명도는 <c>RectNodeState</c>에 없다.
    /// null이면 불투명도 채널은 건너뛴다.
    /// </param>
    public static RectNodeTree Pose(RectNodeTree template, MotionAsset motion, double seconds, Func<CurveRef, bool>? isMuted = null, string keyPrefix = "", IDictionary<string, float>? alphas = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(motion);
        keyPrefix ??= "";

        RectNodeTree tree = template.Clone();
        float t = (float)Math.Max(0, seconds);

        foreach (ChannelAsset channel in (motion.Flattened ?? motion).Channels)
        {
            // 세운 것에 없는 노드는 건너뛴다 — 검증이 이미 소리를 낸다. 여기서 던지면 편집 중 화면이 죽는다.
            string key = keyPrefix + channel.Node;

            if (string.IsNullOrEmpty(channel.Node) || !tree.Contains(key))
            {
                continue;
            }

            float? x = Sample(channel, Axis.X, t, isMuted);
            float? y = Sample(channel, Axis.Y, t, isMuted);
            float? z = Sample(channel, Axis.Z, t, isMuted);

            if (x is null && y is null && z is null)
            {
                continue;
            }

            if (channel.Kind == MotionChannelKind.Alpha)
            {
                if (alphas is not null && x is { } alpha)
                {
                    alphas[key] = Math.Clamp(alpha, 0f, 1f);
                }

                continue;
            }

            RectNodeState state = tree.GetState(key);
            tree.SetState(key, Apply(channel.Kind, state, x, y, z));
        }

        return tree;
    }

    /// <summary>그 성분의 그 시각의 값 — 키가 없거나 뮤트됐으면 null(가만히 선 값 그대로).</summary>
    private static float? Sample(ChannelAsset channel, Axis axis, float t, Func<CurveRef, bool>? isMuted)
    {
        if (channel.Get(axis) is not { Keys.Count: > 0 } curve || isMuted?.Invoke(new CurveRef(channel.Node, channel.Kind, axis)) == true)
        {
            return null;
        }

        return CurveFunctions.Evaluate(TangentMath.ToCurveKeys(curve.Keys), t);
    }

    /// <summary>채널 종류가 노드 상태의 어느 자리를 쓰는가 — 값이 없는(null) 성분은 그 상태의 값 그대로.</summary>
    private static RectNodeState Apply(MotionChannelKind kind, in RectNodeState state, float? x, float? y, float? z) => kind switch
    {
        MotionChannelKind.Position => state.WithAnchoredPosition(new Vec2(x ?? state.AnchoredPosition.X, y ?? state.AnchoredPosition.Y)),
        MotionChannelKind.Scale => state.WithLocalScale(new Vec3(x ?? state.LocalScale.X, y ?? state.LocalScale.Y, state.LocalScale.Z)),
        MotionChannelKind.Euler => state.WithLocalEuler(new Vec3(x ?? state.LocalEulerAngles.X, y ?? state.LocalEulerAngles.Y, z ?? state.LocalEulerAngles.Z)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "모르는 채널 종류"),
    };
}
