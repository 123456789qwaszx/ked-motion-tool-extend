namespace Ked.Presentation.Core
{
    // ─────────────────────────────────────────────────────────────────
    // 깊이의 눈금 — far·back·mid·front·close는 깊이 커브(depth.json의 "level") 위의 수에 붙인 이름표다.
    // 튜닝 값이 아니다(튜닝은 커브가 진다). 눈금을 옮기고 싶으면 여기 숫자 하나만 고치면 된다.
    // ─────────────────────────────────────────────────────────────────
    public static class DepthLevelLabels
    {
        public const float Far = 0f;
        public const float Back = 10f;
        public const float Mid = 14f;
        public const float Front = 16f;
        public const float Close = 20f;
    }
}
