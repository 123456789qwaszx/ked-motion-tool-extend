namespace Ked.Presentation.Core
{
    // ─────────────────────────────────────────────────────────────────
    // 컷의 깊이 계산 — 실제 카메라와 깊이 없이 UI로 카메라를 흉내 내는 그 계산을 컷의 오브젝트에 준다.
    //
    // 맨 위 오브젝트의 깊이(depthLevel, 수)가 두 가지를 정한다:
    //   · 크기 — 깊이 커브(presets/depth.json의 level.scaleCurve). mid에서 지은 그대로, 멀수록 작게.
    //   · 카메라에 따르는 정도 — 밀면(pan) 먼 것은 덜·가까운 것은 더 따라오고, 당기면(zoom) 먼 것은 덜·가까운 것은 더 커진다.
    //     층별 상수(PresentationResponseProfile.Depth*)를 깊이의 눈금 사이에서 곧게 이어 쓴다 — 수가 층 사이에 있어도 끊기지 않는다.
    // ─────────────────────────────────────────────────────────────────
    public static class CutMath
    {
        /// <summary>깊이를 안 준 오브젝트의 깊이 — mid.</summary>
        public const float DefaultDepth = DepthLevelLabels.Mid;

        // 눈금(가까워지는 차례)과 그 층의 응답 — pan을 따라가는 비율(x) · zoom 배율의 지수.
        // 원본: ked-presentation-runtime Runtime/ShotResponse/Core/PresentationResponseProfile.cs.
        private static readonly float[] Levels = { DepthLevelLabels.Far, DepthLevelLabels.Back, DepthLevelLabels.Mid, DepthLevelLabels.Front, DepthLevelLabels.Close };
        private static readonly float[] PanResponse = { -0.04f, -0.02f, 0f, 0.02f, 0.2f };
        private static readonly float[] ZoomResponse = { -0.15f, -0.08f, 0f, 0.04f, 0.16f };

        /// <summary>
        /// 그 깊이의 오브젝트가 카메라에 **더** 따르는 정도 — 밀기에 곱해 더해지는 비율(x만: 세로로는 층마다 다르지 않다)과, 당기기 배율에 얹는 지수.
        /// 눈금 사이는 곧게 잇고, 양 끝 밖은 끝의 값이다.
        /// </summary>
        public static void ResponseAt(float level, out float panResponseX, out float zoomScaleResponse)
        {
            int last = Levels.Length - 1;

            if (level <= Levels[0])
            {
                panResponseX = PanResponse[0];
                zoomScaleResponse = ZoomResponse[0];
                return;
            }

            for (int i = 1; i <= last; i++)
            {
                if (level <= Levels[i])
                {
                    float t = (level - Levels[i - 1]) / (Levels[i] - Levels[i - 1]);
                    panResponseX = PanResponse[i - 1] + (PanResponse[i] - PanResponse[i - 1]) * t;
                    zoomScaleResponse = ZoomResponse[i - 1] + (ZoomResponse[i] - ZoomResponse[i - 1]) * t;
                    return;
                }
            }

            panResponseX = PanResponse[last];
            zoomScaleResponse = ZoomResponse[last];
        }

        /// <summary>깊이에 따른 크기 — mid에서 1(지은 그대로), 멀수록 작게. 깊이 커브가 없으면 1.</summary>
        public static float SizeAt(float level, DepthLevelTuningDto depth)
        {
            if (depth == null || !depth.TryResolve(level, out _, out float at) || !depth.TryResolve(DefaultDepth, out _, out float mid) || mid <= 1e-6f)
                return 1f;

            return at / mid;
        }
    }
}
