using Mt.Authoring.Graph;
using Mt.Authoring.Model;

namespace Mt.Authoring.Editing;

// 재생 블록 명령(PLAN-set.md S10) — "품은 세트의 애니메이션을 언제 튼다". 각각 되돌리기 한 칸. 시작 시각 오름차순은 편집기가 지킨다.
public sealed partial class DocumentEditor
{
    /// <summary>
    /// 재생 블록을 놓고 번호를 돌려준다. 인스턴스는 그 세트가 품은 오브젝트여야 하고, 애니메이션은 품은 세트의 것이어야 하고,
    /// 블록은 이 애니메이션의 길이 안에 들어야 하며 같은 인스턴스의 다른 블록과 겹치면 안 된다.
    /// </summary>
    public int AddPlayBlock(string timelineName, string node, string childTimeline, float at, int repeat = 1)
    {
        TimelineAsset timeline = Require(timelineName);
        var added = new PlayBlockAsset { Object = node, Timeline = childTimeline, At = Math.Max(0f, at), Repeat = repeat };
        RequirePlayFits(timeline, added, except: null);
        Mutate(DocumentChangeKind.Keys, () => InsertPlay(timeline, added));
        return timeline.Plays.IndexOf(added);
    }

    /// <summary>재생 블록을 고친다 — 시각이 바뀌면 자리도 옮겨지고 새 번호를 돌려준다.</summary>
    public int UpdatePlayBlock(string timelineName, int index, string node, string childTimeline, float at, int repeat)
    {
        TimelineAsset timeline = Require(timelineName);
        PlayBlockAsset old = RequirePlay(timeline, index);
        var next = new PlayBlockAsset { Object = node, Timeline = childTimeline, At = Math.Max(0f, at), Repeat = repeat };

        if (old.Object == next.Object && old.Timeline == next.Timeline && old.At == next.At && old.Repeat == next.Repeat)
        {
            return index;
        }

        RequirePlayFits(timeline, next, except: old);
        Mutate(DocumentChangeKind.Keys, () =>
        {
            timeline.Plays.Remove(old);
            InsertPlay(timeline, next);
        });
        return timeline.Plays.IndexOf(next);
    }

    public void RemovePlayBlock(string timelineName, int index)
    {
        TimelineAsset timeline = Require(timelineName);
        PlayBlockAsset target = RequirePlay(timeline, index);
        Mutate(DocumentChangeKind.Keys, () => timeline.Plays.Remove(target));
    }

    /// <summary>
    /// 그 인스턴스의 재생 블록들을 **따로 뗀다** — 블록들이 구워 내던 키를 이 애니메이션의 제 키(경로 이름의 채널)로 굳히고 블록을 지운다.
    /// 재생은 그대로이고, 그 뒤로는 품은 애니메이션을 고쳐도 여기는 안 따라온다(여기서만 다르게 고칠 수 있다). 굳힌 채널의 수를 돌려준다.
    /// 같은 인스턴스의 블록은 이어져 한 커브로 구워지므로(앞 블록의 마지막 값이 다음 블록까지 남는다) 하나만 뗄 수는 없다 — 그 인스턴스의 것을 다 뗀다.
    /// </summary>
    public int DetachPlayBlocks(string timelineName, string node)
    {
        TimelineAsset timeline = Require(timelineName);
        List<PlayBlockAsset> plays = timeline.Plays.Where(p => p.Object == node).ToList();

        if (plays.Count == 0)
        {
            throw new InvalidOperationException($"'{node}'의 재생 블록이 없다.");
        }

        // 그 블록들만 가진 빈 애니메이션을 구워, 나온 채널·이벤트를 제 것으로 들인다.
        var probe = new TimelineAsset { Name = timeline.Name, Set = timeline.Set, Duration = timeline.Duration, Plays = plays };
        TimelineAsset baked = Preview.TimelineFlatten.Flatten(Document, probe);
        int count = 0;

        Mutate(DocumentChangeKind.Keys, () =>
        {
            foreach (ChannelAsset channel in baked.Channels)
            {
                ChannelAsset? own = timeline.FindChannel(channel.Object, channel.Kind);

                if (own is null)
                {
                    timeline.Channels.Add(channel);
                    count++;
                    continue;
                }

                // 이미 제 키가 있는 성분은 그것이 이기고 있었다 — 비어 있는 성분만 채운다.
                own.X ??= channel.X;
                own.Y ??= channel.Y;
                own.Z ??= channel.Z;
                count++;
            }

            timeline.Events.AddRange(baked.Events);
            timeline.Events.Sort((a, b) => a.T.CompareTo(b.T));
            timeline.Plays.RemoveAll(plays.Contains);
        });

        return count;
    }

    private static void InsertPlay(TimelineAsset timeline, PlayBlockAsset added)
    {
        int at = timeline.Plays.Count;

        for (int i = 0; i < timeline.Plays.Count; i++)
        {
            if (timeline.Plays[i].At > added.At)
            {
                at = i;
                break;
            }
        }

        timeline.Plays.Insert(at, added);
    }

    private static PlayBlockAsset RequirePlay(TimelineAsset timeline, int index)
        => index >= 0 && index < timeline.Plays.Count ? timeline.Plays[index] : throw new ArgumentOutOfRangeException(nameof(index), $"재생 블록 {index}번이 없다.");

    private void RequirePlayFits(TimelineAsset timeline, PlayBlockAsset play, PlayBlockAsset? except)
    {
        if (Document.FindSetOrCut(timeline.Set)?.Find(play.Object) is not { Set: { Length: > 0 } inner })
        {
            throw new InvalidOperationException($"'{play.Object}'는 세트 '{timeline.Set}'가 품은 오브젝트가 아니다 — 재생 블록은 품은 세트의 애니메이션을 튼다.");
        }

        if (Document.Find(play.Timeline) is not { } child || child.Set != inner)
        {
            throw new InvalidOperationException($"애니메이션 '{play.Timeline}'은 품은 세트 '{inner}'의 것이 아니다.");
        }

        if (play.Repeat < 1 || play.Repeat > 99)
        {
            throw new InvalidOperationException($"되풀이는 1~99번이다 ('{play.Repeat}')");
        }

        float end = play.At + child.Duration * play.Repeat;

        if (end > timeline.Duration + KeyTimes.Tolerance)
        {
            throw new InvalidOperationException($"블록이 {end:0.###}초에 끝난다 — 이 애니메이션의 길이({timeline.Duration:0.###}초)를 넘는다. 길이를 늘리거나 블록을 당긴다.");
        }

        foreach (PlayBlockAsset other in timeline.Plays.Where(p => !ReferenceEquals(p, except) && p.Object == play.Object))
        {
            float otherEnd = other.At + (Document.Find(other.Timeline)?.Duration ?? 0f) * other.Repeat;

            if (play.At < otherEnd - 1e-4f && other.At < end - 1e-4f)
            {
                throw new InvalidOperationException($"'{play.Object}'의 다른 블록({other.Timeline} @ {other.At:0.###}초)과 겹친다.");
            }
        }
    }
}
