using Ked.Presentation.Core;
using Mt.Authoring.Graph;
using Mt.Authoring.Model;

namespace Mt.Authoring.Validation;

/// <summary>
/// 검증 문제 하나. 모션 이름이 비어 있으면 문서 수준의 문제다.
/// <see cref="Blocking"/>이 false면 **알림**이다 — 내보내기를 막지 않는다.
/// </summary>
public sealed record MotionProblem(string MotionName, string Message, bool Blocking = true)
{
    public override string ToString()
        => string.IsNullOrEmpty(MotionName) ? Message : $"'{MotionName}': {Message}";
}

/// <summary>
/// 편집 중 계속 돌리는 검증 — 컷의 타임라인과 세트의 애니메이션이 컷 파일로 나갈 수 있는 모양인가.
/// 규칙은 여기 한 벌이다(문제는 처음 것 하나가 아니라 **전부** 낸다):
/// <list type="bullet">
/// <item>이름(<c>[a-z0-9_]+</c>, 겹치지 않게) · 길이(0보다 크게) · 얹히는 세트/컷과 오브젝트가 있는가.</item>
/// <item>채널 — 같은 오브젝트·종류는 하나, 종류에 맞는 성분만(회전만 Z, 불투명도는 X만), 불투명도는 0~1이고 받을 수 있는 오브젝트에만.</item>
/// <item>키 — 수(유한한 값)여야 하고 시각이 오름차순이어야 한다. 길이 밖의 키는 막지 않고 알린다(재생 구간 밖이다).</item>
/// <item>이벤트 — 이름 규칙, 시각이 길이 안이고 오름차순.</item>
/// <item>재생 블록 — 사라진 인스턴스·애니메이션, 길이를 넘는 블록은 막지 않고 알린다.</item>
/// </list>
/// 키의 **자리와 수**는 묻지 않는다 — 키가 처음과 끝에 없어도, 하나뿐이어도 된다(앞뒤로는 그 키의 값이 이어진다). 키의 값도 작가의 뜻이다.
/// </summary>
public static class MotionValidator
{
    /// <param name="rigNodeIds">rigKind → 노드 id 목록. null이면 세트·컷과 오브젝트의 존재 검사를 생략한다(세운 리그 없이 문서만 볼 때).</param>
    /// <param name="alphaNodeIds">rigKind → 불투명도를 받을 수 있는 노드. 주면 알파 채널이 그 노드에 놓였는지 본다.</param>
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

            problems.AddRange(ValidateMotion(motion, rigNodeIds, alphaNodeIds));

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

    public static IReadOnlyList<MotionProblem> ValidateMotion(
        MotionAsset motion,
        IReadOnlyDictionary<string, ICollection<string>>? rigNodeIds = null,
        IReadOnlyDictionary<string, ICollection<string>>? alphaNodeIds = null)
    {
        ArgumentNullException.ThrowIfNull(motion);

        var problems = new List<MotionProblem>();
        void Block(string message) => problems.Add(new MotionProblem(motion.Name, message));
        void Notice(string message) => problems.Add(new MotionProblem(motion.Name, message, Blocking: false));

        if (!MotionNames.IsValid(motion.Name))
        {
            Block($"이름은 [a-z0-9_]+ 여야 한다 ('{motion.Name}')");
        }

        if (!(motion.Duration > 0f) || !float.IsFinite(motion.Duration))
        {
            Block($"길이가 0보다 커야 한다 ({motion.Duration})");
        }

        ICollection<string>? nodeIds = null;

        if (rigNodeIds is not null && !rigNodeIds.TryGetValue(motion.Rig ?? "", out nodeIds))
        {
            Block($"rigKind '{motion.Rig}'인 리그(세트 · 컷)가 없다. 있는 것: [{string.Join(", ", rigNodeIds.Keys)}]");
            return problems;
        }

        ICollection<string>? alphaNodes = null;
        alphaNodeIds?.TryGetValue(motion.Rig ?? "", out alphaNodes);
        var seen = new HashSet<(string, MotionChannelKind)>();

        for (int i = 0; i < motion.Channels.Count; i++)
        {
            ChannelAsset channel = motion.Channels[i];

            if (string.IsNullOrEmpty(channel.Node))
            {
                Block($"채널 {i}: 노드가 비어 있다");
                continue;
            }

            if (nodeIds is not null && !nodeIds.Contains(channel.Node))
            {
                Block($"채널 {i}: 이 세트·컷에 없는 오브젝트다 ('{channel.Node}')");
            }

            if (!Enum.IsDefined(channel.Kind))
            {
                Block($"채널 {i}: 모르는 채널 종류다 ({(int)channel.Kind})");
                continue;
            }

            if (!seen.Add((channel.Node, channel.Kind)))
            {
                Block($"채널 {i}: 같은 노드·종류의 채널이 둘이다 ({channel.Node} {channel.Kind})");
            }

            if (channel.Kind != MotionChannelKind.Euler && channel.Z is not null)
            {
                Block($"채널 {i}: {channel.Kind}에는 Z 성분이 없다");
            }

            if (channel.Kind == MotionChannelKind.Alpha)
            {
                if (channel.Y is not null)
                {
                    Block($"채널 {i}: Alpha에는 X 성분만 있다");
                }

                // 불투명도는 0~1이다 — 범위 밖의 키는 재생이 죄어 버려 보이는 것과 파일이 달라진다.
                if (channel.X?.Keys.FirstOrDefault(k => k.V < -KeyTimes.Tolerance || k.V > 1f + KeyTimes.Tolerance) is { } outside)
                {
                    Block($"채널 {i}: Alpha 키 값은 0~1이어야 한다 ({outside.V})");
                }

                if (alphaNodes is not null && !alphaNodes.Contains(channel.Node))
                {
                    Block($"채널 {i}: '{channel.Node}'는 불투명도를 받을 수 없는 노드다(그 오브젝트의 불투명도 받기가 꺼져 있다). 받을 수 있는 노드: [{string.Join(", ", alphaNodes)}]");
                }
            }

            foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
            {
                if (channel.Get(axis) is not { Keys.Count: > 0 } curve)
                {
                    continue;
                }

                List<KeyAsset> keys = curve.Keys;

                // 수가 아닌 값(NaN · 무한)은 컷 파일에 적을 수 없다.
                if (keys.Any(k => !float.IsFinite(k.T) || !float.IsFinite(k.V) || !float.IsFinite(k.InTangent) || !float.IsFinite(k.OutTangent)))
                {
                    Block($"채널 {i}: {axis}: 키의 시각·값·기울기는 수여야 한다(NaN·무한은 컷 파일에 적을 수 없다)");
                    continue;
                }

                for (int k = 1; k < keys.Count; k++)
                {
                    if (keys[k].T <= keys[k - 1].T)
                    {
                        Block($"채널 {i}: {axis}: 키 시각이 오름차순이 아니다 (index {k})");
                        break;
                    }
                }

                int beyond = keys.Count(k => k.T < -KeyTimes.Tolerance || k.T > motion.Duration + KeyTimes.Tolerance);

                if (beyond > 0)
                {
                    Notice($"채널 {i}: {axis}: 길이(0~{motion.Duration:0.###}초) 밖에 키가 {beyond}개 있다 — 그 자리는 재생되지 않는다");
                }
            }
        }

        for (int i = 0; i < motion.Events.Count; i++)
        {
            MotionEventAsset e = motion.Events[i];

            if (!MotionNames.IsValid(e.Name))
            {
                Block($"이벤트 {i}: 이름은 [a-z0-9_]+ 여야 한다 ('{e.Name}')");
            }

            if (!float.IsFinite(e.T) || e.T < -KeyTimes.Tolerance || e.T > motion.Duration + KeyTimes.Tolerance)
            {
                Block($"이벤트 {i}: 시각이 0~길이 밖이다 ({e.T})");
            }

            if (i > 0 && e.T < motion.Events[i - 1].T)
            {
                Block($"이벤트 {i}: 시각이 오름차순이 아니다");
            }
        }

        return problems;
    }

    /// <summary>내보내기를 막는 것만.</summary>
    public static IEnumerable<MotionProblem> Blocking(IEnumerable<MotionProblem> problems)
        => problems.Where(p => p.Blocking);

    /// <summary>
    /// 그 컷을 컷 파일로 내보내는 것을 막는 문제들 — 그 컷의 타임라인과, 문서의 세트·컷에 얹힌 애니메이션(재생 블록으로 구워져 들어올 수 있다)의 막는 문제.
    /// 앱의 "컷 내보내기"와 CLI의 <c>mt cut</c>이 같은 문을 지난다.
    /// </summary>
    public static List<MotionProblem> BlockingCutExport(
        MotionDocument document,
        string cutName,
        IReadOnlyDictionary<string, ICollection<string>>? rigNodeIds,
        IReadOnlyDictionary<string, ICollection<string>>? alphaNodeIds)
        => Blocking(Validate(document, rigNodeIds, alphaNodeIds))
            .Where(p => document.Find(p.MotionName)?.Rig is { } rig && (rig == cutName || document.FindRig(rig) is not null)).ToList();
}
