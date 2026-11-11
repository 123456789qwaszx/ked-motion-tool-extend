using System;
using System.Collections.Generic;

namespace Ked.Presentation.Core
{
    // ─────────────────────────────────────────────────────────────────
    // 깊이 커브 파일(depth.json)의 전송 타입.
    // 모양은 {"MonoBehaviour": {"level": {"yCurve": …, "scaleCurve": …}}}이고, 그 밖의 필드는 읽지 않는다.
    // 커브는 { "m_Curve": [ {time,value,inSlope,outSlope}, … ] } — 구간 밖은 끝의 기울기로 곧게 잇는다(CurveFunctions.EvaluateUnclamped).
    // ─────────────────────────────────────────────────────────────────

    [Serializable]
    public sealed class DepthTuningFileDto
    {
        public DepthTuningBodyDto MonoBehaviour;
    }

    [Serializable]
    public sealed class DepthTuningBodyDto
    {
        /// <summary>깊이(수)에서 세로 자리와 크기를 내는 커브.</summary>
        public DepthLevelTuningDto level;
    }

    [Serializable]
    public sealed class DepthLevelTuningDto
    {
        public AnimationCurveDto yCurve;
        public AnimationCurveDto scaleCurve;

        public bool TryResolve(float level, out float depthY, out float depthScale)
        {
            depthY = 0f;
            depthScale = 0f;

            CurveKey[] yKeys = yCurve?.ToKeys();
            CurveKey[] scaleKeys = scaleCurve?.ToKeys();

            if (yKeys == null || yKeys.Length == 0 || scaleKeys == null || scaleKeys.Length == 0)
                return false;

            depthY = CurveFunctions.EvaluateUnclamped(yKeys, level);

            // 크기의 하한 — 0이나 음수로 떨어지지 않게 한다.
            float rawScale = CurveFunctions.EvaluateUnclamped(scaleKeys, level);
            depthScale = rawScale < 0.0001f ? 0.0001f : rawScale;

            return true;
        }
    }

    [Serializable]
    public sealed class AnimationCurveDto
    {
        public List<KeyframeDto> m_Curve = new();

        public CurveKey[] ToKeys()
        {
            if (m_Curve == null || m_Curve.Count == 0)
                return null;

            CurveKey[] keys = new CurveKey[m_Curve.Count];

            for (int i = 0; i < m_Curve.Count; i++)
            {
                KeyframeDto k = m_Curve[i];
                keys[i] = new CurveKey(k.time, k.value, k.inSlope, k.outSlope);
            }

            return keys;
        }
    }

    [Serializable]
    public sealed class KeyframeDto
    {
        public float time;
        public float value;
        public float inSlope;
        public float outSlope;
    }
}
