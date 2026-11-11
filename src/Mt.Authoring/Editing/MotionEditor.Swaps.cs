using Ked.Presentation.Core;
using Mt.Authoring.Graph;
using Mt.Authoring.Model;

namespace Mt.Authoring.Editing;

// 그림 키의 명령(PLAN-standalone.md C4) — 계단 키: 그 프레임부터 그 오브젝트의 그림이 바뀐다(표정). 각각 되돌리기 한 칸. 시각 오름차순은 편집기가 지킨다.
public sealed partial class MotionEditor
{
    /// <summary>
    /// 그림 키를 놓고 번호를 돌려준다 — 그 시각부터 그 오브젝트가 그 그림을 그린다(다음 그림 키까지). 같은 오브젝트의 같은 시각에 키가 있으면 그 키를 대신한다.
    /// 그 오브젝트는 그림을 그리는 오브젝트여야 한다(툴이 정의한 세트·컷이면 여기서 본다).
    /// </summary>
    public int AddImageKey(string motionName, string node, float time, string image)
    {
        ArgumentNullException.ThrowIfNull(node);
        MotionAsset motion = Require(motionName);
        string path = (image ?? "").Trim().Replace('\\', '/');

        if (path.Length == 0)
        {
            throw new InvalidOperationException("그림 키에는 그림이 있어야 한다.");
        }

        if (!float.IsFinite(time) || time < 0f || time > motion.Duration + KeyTimes.Tolerance)
        {
            throw new InvalidOperationException($"그림 키의 시각은 길이(0~{motion.Duration:0.###}초) 안이다 ('{time}')");
        }

        if (Document.FindRig(motion.Rig) is { } rig && !node.Contains(RigAssetCodec.PathSeparator, StringComparison.Ordinal) && rig.Find(node) is not { Image: true })
        {
            throw new InvalidOperationException($"'{node}'는 그림을 그리는 오브젝트가 아니다.");
        }

        var added = new MotionSwapAsset { T = time, Node = node, Image = path };

        Mutate(MotionChangeKind.Metadata, () =>
        {
            motion.Swaps.RemoveAll(s => s.Node == node && Math.Abs(s.T - time) <= KeyTimes.Tolerance);
            int at = motion.Swaps.FindIndex(s => s.T > time);
            motion.Swaps.Insert(at < 0 ? motion.Swaps.Count : at, added);
        });

        return motion.Swaps.IndexOf(added);
    }

    public void RemoveImageKey(string motionName, int index)
    {
        MotionAsset motion = Require(motionName);

        if (index < 0 || index >= motion.Swaps.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"그림 키 {index}가 '{motionName}'에 없다.");
        }

        Mutate(MotionChangeKind.Metadata, () => motion.Swaps.RemoveAt(index));
    }
}
