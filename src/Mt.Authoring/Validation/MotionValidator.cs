using Ked.Presentation.Core;
using Mt.Authoring.Model;

namespace Mt.Authoring.Validation;

/// <summary>
/// 검증 문제 하나. 모션 이름이 비어 있으면 문서 수준의 문제다.
/// <see cref="Blocking"/>이 false면 **경고**다 — 내보내기를 막지 않는다(규칙이 아니라 "튄다"는 알림).
/// </summary>
public sealed record MotionProblem(string MotionName, string Message, bool Blocking = true)
{
    public override string ToString()
        => string.IsNullOrEmpty(MotionName) ? Message : $"'{MotionName}': {Message}";
}

/// <summary>
/// 편집 중 계속 돌리는 검증. 규칙은 코어 <see cref="MotionRules"/> 한 벌뿐이다 — 런타임 로더와 Unity 내보내기가
/// 같은 규칙을 쓰므로 "툴에서는 되는데 재생에서 사라지는" 모션이 안 생긴다. 문구도 코어의 것을 그대로 보인다.
///
/// 여기서 더 보는 것은 문서 수준(같은 이름 둘, 리그 덤프에 없는 rigKind)이다.
/// 키의 **값**은 알리지 않는다 — 첫 키가 중립값이 아닌 것("지금 값 +140에서 시작")도, 복원(restore)이 중립값이 아닌 데서 끝나는 것(끝에서 시작값으로 돌아간다 —
/// 한 방향으로 계속 도는 되풀이 머묾)도 작가의 뜻이다. (복원의 끝 값을 경고하던 것은 2026-11-10 소유자 결정으로 없앴다.)
/// </summary>
public static class MotionValidator
{
    /// <param name="rigNodeIds">rigKind → 노드 id 목록. null이면 노드 존재 검사를 생략한다(덤프가 아직 없는 자리).</param>
    /// <param name="alphaNodeIds">rigKind → 불투명도를 받을 수 있는 노드(덤프의 hasCanvasGroup). 주면 알파 채널이 그 노드에 놓였는지 본다.</param>
    public static IReadOnlyList<MotionProblem> Validate(
        MotionDocument document,
        IReadOnlyDictionary<string, ICollection<string>>? rigNodeIds = null,
        IReadOnlyDictionary<string, ICollection<string>>? alphaNodeIds = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var problems = new List<MotionProblem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (MotionAsset motion in document.Motions)
        {
            if (!string.IsNullOrEmpty(motion.Name) && !seen.Add(motion.Name))
            {
                problems.Add(new MotionProblem(motion.Name, "같은 이름이 둘이다"));
            }

            // 컷의 타임라인과 묶음(세트)의 애니메이션은 키가 처음과 끝에 꼭 있지 않아도 되고 하나뿐이어도 된다(앞뒤로는 그 키의 값이 이어진다) —
            // "0초에서 시작해 길이에서 끝난다"는 옛 산출물(motions.json)의 규칙이다. 컷은 제 형식으로 나가고(PLAN-standalone.md §7) 묶음은 컷에 구워져 나간다.
            // 컷에서 고른 것을 새 세트로 빼면 그 키들이 그대로 세트의 애니메이션이 되므로, 둘의 규칙이 같아야 한다.
            bool cutTimeline = document.FindRig(motion.Rig) is not null; // 툴이 정의한 것(컷 · 세트) — 덤프의 리그(캐릭터)는 옛 규칙 그대로
            problems.AddRange(ValidateMotion(motion, rigNodeIds, alphaNodeIds).Where(p => !cutTimeline || !CutRelaxed.Any(text => p.Message.Contains(text, StringComparison.Ordinal))));

            // 재생 블록 — 인스턴스나 애니메이션이 사라졌으면 그 블록은 구울 때 빠진다(막지는 않고 알린다).
            foreach (MotionPlayAsset play in motion.Plays)
            {
                string? inner = document.FindRig(motion.Rig)?.Find(play.Node)?.Set;

                if (string.IsNullOrEmpty(inner))
                {
                    problems.Add(new MotionProblem(motion.Name, $"재생 블록 {play.At:0.###}초: '{play.Node}'는 이 세트가 품은 세트가 아니다 — 이 블록은 재생되지 않는다", Blocking: false));
                }
                else if (document.Find(play.Motion) is not { } child || child.Rig != inner)
                {
                    problems.Add(new MotionProblem(motion.Name, $"재생 블록 {play.At:0.###}초: 애니메이션 '{play.Motion}'이 세트 '{inner}'에 없다 — 이 블록은 재생되지 않는다", Blocking: false));
                }
                else if (play.At + child.Duration * Math.Max(1, play.Repeat) > motion.Duration + 1e-3f)
                {
                    // 놓을 때는 막지만, 품은 애니메이션을 나중에 길게 고치면(또는 이 애니메이션을 짧게 고치면) 넘친다 — 넘친 뒤쪽은 잘려 재생된다.
                    problems.Add(new MotionProblem(motion.Name, $"재생 블록 {play.At:0.###}초: '{play.Motion}'({child.Duration * Math.Max(1, play.Repeat):0.###}초)이 이 애니메이션의 길이({motion.Duration:0.###}초)를 넘는다 — 뒤쪽이 잘린다", Blocking: false));
                }
            }
        }

        return problems;
    }

    /// <summary>컷의 타임라인과 세트의 애니메이션에는 걸지 않는 모양 규칙의 문구(코어 <see cref="MotionRules"/>의 것).</summary>
    private static readonly string[] CutRelaxed = { "키가 2개 이상이어야 한다", "첫 키 시각이 0이 아니다", "마지막 키 시각이 길이와 다르다" };

    public static IReadOnlyList<MotionProblem> ValidateMotion(
        MotionAsset motion,
        IReadOnlyDictionary<string, ICollection<string>>? rigNodeIds = null,
        IReadOnlyDictionary<string, ICollection<string>>? alphaNodeIds = null)
    {
        ArgumentNullException.ThrowIfNull(motion);

        var problems = new List<MotionProblem>();
        ICollection<string>? nodeIds = null;

        if (rigNodeIds is not null)
        {
            if (!rigNodeIds.TryGetValue(motion.Rig ?? "", out nodeIds))
            {
                problems.Add(new MotionProblem(
                    motion.Name,
                    $"리그 덤프에 rigKind '{motion.Rig}'가 없다. 있는 것: [{string.Join(", ", rigNodeIds.Keys)}]"));
                return problems;
            }
        }

        MotionClip clip = MotionAssetCodec.ToClip(motion);

        if (!MotionRules.TryValidate(clip, nodeIds, out string reason))
        {
            problems.Add(new MotionProblem(motion.Name, reason));
        }

        // 알파 채널은 불투명도를 받을 수 있는 노드에만 — 호스트가 쓸 CanvasGroup이 없으면 재생에서 아무 일도 안 일어난다.
        if (alphaNodeIds is not null && alphaNodeIds.TryGetValue(motion.Rig ?? "", out ICollection<string>? alphaNodes))
        {
            for (int i = 0; i < motion.Channels.Count; i++)
            {
                if (motion.Channels[i].Kind == MotionChannelKind.Alpha && !alphaNodes.Contains(motion.Channels[i].Node))
                {
                    problems.Add(new MotionProblem(
                        motion.Name,
                        $"채널 {i}: '{motion.Channels[i].Node}'는 불투명도를 받을 수 없는 노드다(덤프에 CanvasGroup이 없다). 받을 수 있는 노드: [{string.Join(", ", alphaNodes)}]"));
                }
            }
        }

        return problems;
    }

    /// <summary>내보내기를 막는 것만.</summary>
    public static IEnumerable<MotionProblem> Blocking(IEnumerable<MotionProblem> problems)
        => problems.Where(p => p.Blocking);
}
