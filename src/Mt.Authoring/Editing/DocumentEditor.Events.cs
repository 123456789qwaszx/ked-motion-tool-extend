using Mt.Authoring.Model;

namespace Mt.Authoring.Editing;

// 이벤트 키 명령 — 각각 되돌리기 한 칸. 시각 오름차순은 편집기가 지킨다. 이름 규칙·길이 밖은 검증이 알린다.
public sealed partial class DocumentEditor
{
    /// <summary>이벤트 키를 놓고 번호를 돌려준다. 시각은 0 아래로 못 간다.</summary>
    public int AddEventKey(string timelineName, float time, string name, string? value = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        TimelineAsset timeline = Require(timelineName);
        var added = new EventKeyAsset { T = ClampEventTime(time), Name = name.Trim(), Value = string.IsNullOrEmpty(value) ? null : value };
        Mutate(DocumentChangeKind.Metadata, () => InsertEvent(timeline, added));
        return timeline.Events.IndexOf(added);
    }

    /// <summary>이벤트 키의 시각·이름·값을 바꾼다. 시각이 바뀌면 자리도 옮겨지고 새 번호를 돌려준다.</summary>
    public int UpdateEventKey(string timelineName, int index, float time, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        TimelineAsset timeline = Require(timelineName);
        EventKeyAsset old = RequireEvent(timeline, index);
        var next = new EventKeyAsset { T = ClampEventTime(time), Name = name.Trim(), Value = string.IsNullOrEmpty(value) ? null : value };

        Mutate(DocumentChangeKind.Metadata, () =>
        {
            timeline.Events.Remove(old);
            InsertEvent(timeline, next);
        });
        return timeline.Events.IndexOf(next);
    }

    public void RemoveEventKey(string timelineName, int index)
    {
        TimelineAsset timeline = Require(timelineName);
        EventKeyAsset target = RequireEvent(timeline, index);
        Mutate(DocumentChangeKind.Metadata, () => timeline.Events.Remove(target));
    }

    private static void InsertEvent(TimelineAsset timeline, EventKeyAsset added)
    {
        int at = timeline.Events.Count;

        for (int i = 0; i < timeline.Events.Count; i++)
        {
            if (timeline.Events[i].T > added.T)
            {
                at = i;
                break;
            }
        }

        timeline.Events.Insert(at, added);
    }

    private static EventKeyAsset RequireEvent(TimelineAsset timeline, int index)
    {
        if (index < 0 || index >= timeline.Events.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"타임라인 '{timeline.Name}'의 이벤트가 {timeline.Events.Count}개다.");
        }

        return timeline.Events[index];
    }

    private static float ClampEventTime(float time)
    {
        if (!float.IsFinite(time))
        {
            throw new InvalidOperationException($"이벤트 키의 시각은 수여야 한다 ('{time}')");
        }

        return Math.Max(0f, time);
    }
}
