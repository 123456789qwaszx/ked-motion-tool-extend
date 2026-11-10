using Mt.Authoring.Model;

namespace Mt.Authoring.Editing;

// 재생 블록 명령(PLAN-set.md S10) — "품은 세트의 애니메이션을 언제 튼다". 각각 되돌리기 한 칸. 시작 시각 오름차순은 편집기가 지킨다.
public sealed partial class MotionEditor
{
    /// <summary>
    /// 재생 블록을 놓고 번호를 돌려준다. 인스턴스는 그 세트가 품은 오브젝트여야 하고, 애니메이션은 품은 세트의 것이어야 하고,
    /// 블록은 이 애니메이션의 길이 안에 들어야 하며 같은 인스턴스의 다른 블록과 겹치면 안 된다.
    /// </summary>
    public int AddMotionPlay(string motionName, string node, string childMotion, float at, int repeat = 1)
    {
        MotionAsset motion = Require(motionName);
        var added = new MotionPlayAsset { Node = node, Motion = childMotion, At = Math.Max(0f, at), Repeat = repeat };
        RequirePlayFits(motion, added, except: null);
        Mutate(MotionChangeKind.Keys, () => InsertPlay(motion, added));
        return motion.Plays.IndexOf(added);
    }

    /// <summary>재생 블록을 고친다 — 시각이 바뀌면 자리도 옮겨지고 새 번호를 돌려준다.</summary>
    public int UpdateMotionPlay(string motionName, int index, string node, string childMotion, float at, int repeat)
    {
        MotionAsset motion = Require(motionName);
        MotionPlayAsset old = RequirePlay(motion, index);
        var next = new MotionPlayAsset { Node = node, Motion = childMotion, At = Math.Max(0f, at), Repeat = repeat };

        if (old.Node == next.Node && old.Motion == next.Motion && old.At == next.At && old.Repeat == next.Repeat)
        {
            return index;
        }

        RequirePlayFits(motion, next, except: old);
        Mutate(MotionChangeKind.Keys, () =>
        {
            motion.Plays.Remove(old);
            InsertPlay(motion, next);
        });
        return motion.Plays.IndexOf(next);
    }

    public void RemoveMotionPlay(string motionName, int index)
    {
        MotionAsset motion = Require(motionName);
        MotionPlayAsset target = RequirePlay(motion, index);
        Mutate(MotionChangeKind.Keys, () => motion.Plays.Remove(target));
    }

    /// <summary>
    /// 그 인스턴스의 재생 블록들을 **따로 뗀다** — 블록들이 구워 내던 키를 이 애니메이션의 제 키(경로 이름의 채널)로 굳히고 블록을 지운다.
    /// 재생은 그대로이고, 그 뒤로는 품은 애니메이션을 고쳐도 여기는 안 따라온다(여기서만 다르게 고칠 수 있다). 굳힌 채널의 수를 돌려준다.
    /// 같은 인스턴스의 블록은 이어져 한 커브로 구워지므로(유지는 쌓인다) 하나만 뗄 수는 없다 — 그 인스턴스의 것을 다 뗀다.
    /// </summary>
    public int DetachMotionPlays(string motionName, string node)
    {
        MotionAsset motion = Require(motionName);
        List<MotionPlayAsset> plays = motion.Plays.Where(p => p.Node == node).ToList();

        if (plays.Count == 0)
        {
            throw new InvalidOperationException($"'{node}'의 재생 블록이 없다.");
        }

        // 그 블록들만 가진 빈 애니메이션을 구워, 나온 채널·이벤트를 제 것으로 들인다.
        var probe = new MotionAsset { Name = motion.Name, Rig = motion.Rig, Duration = motion.Duration, End = motion.End, PreviewMode = motion.PreviewMode, Plays = plays };
        MotionAsset baked = Preview.MotionFlatten.Flatten(Document, probe);
        int count = 0;

        Mutate(MotionChangeKind.Keys, () =>
        {
            foreach (ChannelAsset channel in baked.Channels)
            {
                ChannelAsset? own = motion.FindChannel(channel.Node, channel.Kind);

                if (own is null)
                {
                    motion.Channels.Add(channel);
                    count++;
                    continue;
                }

                // 이미 제 키가 있는 성분은 그것이 이기고 있었다 — 비어 있는 성분만 채운다.
                own.X ??= channel.X;
                own.Y ??= channel.Y;
                own.Z ??= channel.Z;
                count++;
            }

            motion.Events.AddRange(baked.Events);
            motion.Events.Sort((a, b) => a.T.CompareTo(b.T));
            motion.Plays.RemoveAll(plays.Contains);
        });

        return count;
    }

    private static void InsertPlay(MotionAsset motion, MotionPlayAsset added)
    {
        int at = motion.Plays.Count;

        for (int i = 0; i < motion.Plays.Count; i++)
        {
            if (motion.Plays[i].At > added.At)
            {
                at = i;
                break;
            }
        }

        motion.Plays.Insert(at, added);
    }

    private static MotionPlayAsset RequirePlay(MotionAsset motion, int index)
        => index >= 0 && index < motion.Plays.Count ? motion.Plays[index] : throw new ArgumentOutOfRangeException(nameof(index), $"재생 블록 {index}번이 없다.");

    private void RequirePlayFits(MotionAsset motion, MotionPlayAsset play, MotionPlayAsset? except)
    {
        if (Document.FindRig(motion.Rig)?.Find(play.Node) is not { Set: { Length: > 0 } inner })
        {
            throw new InvalidOperationException($"'{play.Node}'는 세트 '{motion.Rig}'가 품은 오브젝트가 아니다 — 재생 블록은 품은 세트의 애니메이션을 튼다.");
        }

        if (Document.Find(play.Motion) is not { } child || child.Rig != inner)
        {
            throw new InvalidOperationException($"애니메이션 '{play.Motion}'은 품은 세트 '{inner}'의 것이 아니다.");
        }

        if (play.Repeat < 1 || play.Repeat > 99)
        {
            throw new InvalidOperationException($"되풀이는 1~99번이다 ('{play.Repeat}')");
        }

        float end = play.At + child.Duration * play.Repeat;

        if (end > motion.Duration + Ked.Presentation.Core.MotionRules.Tolerance)
        {
            throw new InvalidOperationException($"블록이 {end:0.###}초에 끝난다 — 이 애니메이션의 길이({motion.Duration:0.###}초)를 넘는다. 길이를 늘리거나 블록을 당긴다.");
        }

        foreach (MotionPlayAsset other in motion.Plays.Where(p => !ReferenceEquals(p, except) && p.Node == play.Node))
        {
            float otherEnd = other.At + (Document.Find(other.Motion)?.Duration ?? 0f) * other.Repeat;

            if (play.At < otherEnd - 1e-4f && other.At < end - 1e-4f)
            {
                throw new InvalidOperationException($"'{play.Node}'의 다른 블록({other.Motion} @ {other.At:0.###}초)과 겹친다.");
            }
        }
    }
}
