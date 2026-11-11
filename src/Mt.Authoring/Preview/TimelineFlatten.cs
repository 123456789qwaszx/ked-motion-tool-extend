using Mt.Authoring.Graph;
using Mt.Authoring.Model;

namespace Mt.Authoring.Preview;

/// <summary>
/// 재생 블록을 펼쳐 굽는다(<c>PLAN-set.md</c> S10·S11) — 세트의 애니메이션이 "품은 세트의 애니메이션을 언제 튼다"를 담으면,
/// 그 애니메이션의 채널들을 경로 이름(<c>인스턴스.오브젝트</c>)으로 이 애니메이션에 들여 **한 덩어리 타임라인**으로 만든다.
/// 컷 파일에는 이 구운 키가 나간다 — 조립은 툴 안의 일이다.
///
/// 블록의 뜻(키는 "그 프레임의 모습"이다): 블록이 도는 동안은 품은 애니메이션의 키가 그 속성의 값이다. **블록이 끝나면 마지막 값이 남아** 같은 인스턴스의 다음 블록까지 이어지고,
/// **첫 블록 앞에서는 가만히 선 값**이다. 되풀이는 같은 블록을 이어 놓은 것 — 바퀴마다 처음부터 다시 튼다(쌓이지 않는다).
/// 품은 애니메이션에 키가 없는 속성은 건드리지 않는다(가만히 선 값 그대로).
///
/// 커브는 끊길 수 없어(계단 키가 없다) 블록의 처음과 끝에서 값이 튀는 자리는 <see cref="Seam"/>초짜리 짧은 선으로 잇는다.
/// 바깥 애니메이션이 같은 경로에 제 키를 놓았으면 그 성분은 제 키가 이긴다(품은 것을 여기서만 덮어쓴다).
/// </summary>
public static class TimelineFlatten
{
    /// <summary>값이 튀는 자리를 잇는 선의 길이(초).</summary>
    public const float Seam = 1f / 240f;

    private const int MaxDepth = 8;

    /// <summary>문서의 모든 애니메이션의 구운 것을 다시 만든다 — 재생 블록이 없는 것은 null. 편집기가 바뀔 때마다, 문서를 읽은 뒤 부른다.</summary>
    public static void Refresh(AuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Timelines.All(m => m.Plays.Count == 0))
        {
            foreach (TimelineAsset timeline in document.Timelines)
            {
                timeline.Flattened = null;
            }

            return;
        }

        var done = new Dictionary<string, TimelineAsset?>(StringComparer.Ordinal);

        foreach (TimelineAsset timeline in document.Timelines)
        {
            timeline.Flattened = timeline.Plays.Count == 0 ? null : Flatten(document, timeline, done, new HashSet<string>(StringComparer.Ordinal), 0);
        }
    }

    /// <summary>그 애니메이션을 펼쳐 구운 것 — 재생 블록이 없으면 그 애니메이션 그대로.</summary>
    public static TimelineAsset Flatten(AuthoringDocument document, TimelineAsset timeline)
        => timeline.Plays.Count == 0 ? timeline : Flatten(document, timeline, new Dictionary<string, TimelineAsset?>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), 0) ?? timeline;

    private static TimelineAsset? Flatten(AuthoringDocument document, TimelineAsset timeline, Dictionary<string, TimelineAsset?> done, HashSet<string> open, int depth)
    {
        if (done.TryGetValue(timeline.Name, out TimelineAsset? cached))
        {
            return cached;
        }

        if (depth > MaxDepth || !open.Add(timeline.Name))
        {
            return null; // 순환 — 건너뛴다
        }

        var result = new TimelineAsset
        {
            Name = timeline.Name,
            Set = timeline.Set,
            Duration = timeline.Duration,
            Channels = timeline.Channels.Select(CloneChannel).ToList(),
            Events = timeline.Events.Select(e => new EventKeyAsset { T = e.T, Name = e.Name, Value = e.Value }).ToList(),
            ImageKeys = timeline.ImageKeys.Select(s => new ImageKeyAsset { T = s.T, Object = s.Object, Image = s.Image }).ToList(),
        };

        SetAsset? set = document.FindSetOrCut(timeline.Set);
        // 대상 커브(경로 이름의 노드 · 종류 · 성분) → 그 위에 차례로 놓이는 조각들.
        var pieces = new Dictionary<(string Node, ChannelKind Kind, Axis Axis), List<Piece>>();
        var rests = new Dictionary<(string Node, ChannelKind Kind, Axis Axis), float>();

        foreach (PlayBlockAsset play in timeline.Plays.OrderBy(p => p.At))
        {
            if (set?.Find(play.Object) is not { Set: { Length: > 0 } inner } || document.Find(play.Timeline) is not { } source || source.Set != inner || source.Duration <= 0f)
            {
                continue; // 없는 인스턴스·애니메이션 — 검증이 알린다
            }

            TimelineAsset child = source.Plays.Count == 0 ? source : Flatten(document, source, done, open, depth + 1) ?? source;
            string path = play.Object + SetCodec.PathSeparator;

            for (int cycle = 0; cycle < Math.Max(1, play.Repeat); cycle++)
            {
                float start = play.At + cycle * child.Duration;

                foreach (ChannelAsset channel in child.Channels)
                {
                    foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
                    {
                        if (channel.Get(axis) is not { Keys.Count: > 0 } curve)
                        {
                            continue;
                        }

                        var key = (path + channel.Object, channel.Kind, axis);

                        if (!pieces.TryGetValue(key, out List<Piece>? list))
                        {
                            list = new List<Piece>();
                            pieces[key] = list;
                            rests[key] = RestOf(document, inner, channel.Object, channel.Kind, axis);
                        }

                        list.Add(new Piece(start, Spanning(curve.Keys, child.Duration)));
                    }
                }

                // 품은 애니메이션의 그림 키 — 경로 이름으로, 그 바퀴의 시각으로.
                foreach (ImageKeyAsset imageKey in child.ImageKeys)
                {
                    if (start + imageKey.T <= timeline.Duration + KeyTimes.Tolerance)
                    {
                        result.ImageKeys.Add(new ImageKeyAsset { T = Math.Min(start + imageKey.T, timeline.Duration), Object = path + imageKey.Object, Image = imageKey.Image });
                    }
                }

                foreach (EventKeyAsset e in child.Events)
                {
                    if (start + e.T <= timeline.Duration + KeyTimes.Tolerance)
                    {
                        result.Events.Add(new EventKeyAsset { T = Math.Min(start + e.T, timeline.Duration), Name = e.Name, Value = e.Value });
                    }
                }
            }
        }

        foreach (((string node, ChannelKind kind, Axis axis), List<Piece> list) in pieces)
        {
            ChannelAsset? target = result.FindChannel(node, kind);

            if (target?.Get(axis) is not null)
            {
                continue; // 바깥이 이 성분에 제 키를 놓았다 — 그것이 이긴다
            }

            if (target is null)
            {
                target = new ChannelAsset { Object = node, Kind = kind };
                result.Channels.Add(target);
            }

            Set(target, axis, new ComponentCurve { Keys = Bake(list, rests[(node, kind, axis)], timeline.Duration) });
        }

        result.Events = result.Events.OrderBy(e => e.T).ToList();
        result.ImageKeys = result.ImageKeys.OrderBy(s => s.T).ToList();
        open.Remove(timeline.Name);
        done[timeline.Name] = result;
        return result;
    }

    /// <summary>
    /// 커브가 그 애니메이션의 처음(0초)과 끝(길이)까지 닿게 — 키가 처음과 끝에 없으면 첫 키 앞과 마지막 키 뒤로 그 값이 이어진다(재생의 규칙).
    /// 굽기는 조각을 "0초의 키부터 길이의 키까지"로 이어 붙이므로, 그 이어짐을 키로 적어 준다.
    /// </summary>
    private static List<KeyAsset> Spanning(List<KeyAsset> keys, float duration)
    {
        bool head = keys[0].T > KeyTimes.Tolerance;
        bool tail = keys[^1].T < duration - KeyTimes.Tolerance;

        if (!head && !tail)
        {
            return keys;
        }

        var spanned = new List<KeyAsset>();

        if (head)
        {
            // 0초부터 첫 키까지 평평하다 — 첫 키로 들어오는 기울기를 끊는다.
            spanned.Add(new KeyAsset { T = 0f, V = keys[0].V, Tangent = TangentMode.Free, Broken = true });
            spanned.Add(new KeyAsset { T = keys[0].T, V = keys[0].V, InTangent = 0f, OutTangent = keys.Count > 1 ? keys[0].OutTangent : 0f, Tangent = TangentMode.Free, Broken = true });
            spanned.AddRange(keys.Skip(1));
        }
        else
        {
            spanned.AddRange(keys);
        }

        if (tail)
        {
            KeyAsset last = spanned[^1];
            spanned[^1] = new KeyAsset { T = last.T, V = last.V, InTangent = last.InTangent, OutTangent = 0f, Tangent = TangentMode.Free, Broken = true };
            spanned.Add(new KeyAsset { T = duration, V = last.V, Tangent = TangentMode.Free, Broken = true });
        }

        return spanned;
    }

    /// <summary>조각 하나 — 품은 애니메이션의 커브 하나가 놓인 자리(시작 시각과, 0초부터 길이까지 닿는 키들).</summary>
    private sealed record Piece(float Start, List<KeyAsset> Keys);

    /// <summary>
    /// 한 속성에 놓인 조각들을 한 커브로 — 첫 조각 앞은 가만히 선 값(<paramref name="rest"/>), 조각 안은 품은 애니메이션의 키 그대로, 조각 뒤로는 마지막 값이 이어진다.
    /// </summary>
    private static List<KeyAsset> Bake(List<Piece> pieces, float rest, float duration)
    {
        float held = rest; // 지금까지 남은 값 — 첫 조각 앞에서는 가만히 선 값, 그 뒤로는 앞 조각의 마지막 값
        var keys = new List<KeyAsset>();

        // like가 없으면 "그대로 머무는" 키, 있으면 품은 애니메이션의 키. first는 조각의 첫 키 — 앞과는 평평하게 이어진다.
        void Add(float t, float v, KeyAsset? like = null, bool first = false)
        {
            t = Math.Clamp(t, 0f, duration);
            KeyAsset? previous = keys.Count > 0 ? keys[^1] : null;

            // 앞 키와 같은 자리·같은 값이면 한 키로 합친다(이어 붙은 바퀴·블록) — 들어오는 기울기는 앞의 것, 나가는 기울기는 뒤의 것.
            if (previous is not null && Math.Abs(t - previous.T) <= KeyTimes.Tolerance && Math.Abs(v - previous.V) <= 1e-5f)
            {
                previous.OutTangent = like?.OutTangent ?? 0f;
                previous.Tangent = TangentMode.Free;
                previous.Broken = true;
                return;
            }

            // 시각이 겹치거나 거꾸로 가면 조금 뒤로 민다(커브는 시각 오름차순이어야 한다).
            if (previous is not null && t <= previous.T + KeyTimes.Tolerance)
            {
                t = previous.T + Seam;
            }

            if (t > duration + KeyTimes.Tolerance)
            {
                return;
            }

            bool flatIn = like is null || first;

            if (flatIn && previous is not null)
            {
                // 머무는 구간·잇는 선은 곧게 — 앞 키가 나가는 기울기를 끊는다.
                previous.OutTangent = 0f;
                previous.Tangent = TangentMode.Free;
                previous.Broken = true;
            }

            keys.Add(like is null
                ? new KeyAsset { T = Math.Min(t, duration), V = v, Tangent = TangentMode.Free, Broken = true }
                : new KeyAsset { T = Math.Min(t, duration), V = v, InTangent = flatIn ? 0f : like.InTangent, OutTangent = like.OutTangent, Tangent = TangentMode.Free, Broken = true });
        }

        // 0초에서 시작하는 블록은 0초의 값이 그 블록의 것이다 — 가만히 선 값의 키를 먼저 놓으면 첫 프레임에 튄다.
        if (pieces.Min(p => p.Start) > KeyTimes.Tolerance)
        {
            Add(0f, rest);
        }

        foreach (Piece piece in pieces.OrderBy(p => p.Start))
        {
            bool first = true;

            foreach (KeyAsset key in piece.Keys)
            {
                // 블록이 남은 값과 다른 값에서 시작하면, 직전까지는 남은 값 그대로 두고 짧은 선으로 잇는다.
                // 블록의 키는 제 시각에 정확히 선다 — 이음매는 늘 블록의 앞쪽에 둔다(앞 블록·앞 바퀴가 바로 붙어 있으면 그 끝 키를 이음매만큼 당긴다).
                if (first && keys.Count > 0 && Math.Abs(key.V - held) > 1e-5f)
                {
                    float seam = piece.Start - Seam;

                    if (seam > keys[^1].T + KeyTimes.Tolerance)
                    {
                        Add(seam, held);
                    }
                    else if (keys.Count == 1 ? seam >= 0f : seam > keys[^2].T + KeyTimes.Tolerance)
                    {
                        keys[^1].T = seam;
                    }
                }

                Add(piece.Start + key.T, key.V, key, first);
                first = false;
            }

            held = piece.Keys[^1].V; // 블록이 끝나면 마지막 값이 남는다
        }

        if (keys[^1].T < duration - KeyTimes.Tolerance)
        {
            Add(duration, keys[^1].V);
        }
        else
        {
            keys[^1].T = duration;
        }

        return keys;
    }

    /// <summary>품은 세트 안의 그 오브젝트의 가만히 선 값 — 경로(<c>a.b</c>)면 품은 세트를 따라 들어간다. 못 찾으면 중립값.</summary>
    private static float RestOf(AuthoringDocument document, string setName, string node, ChannelKind channel, Axis axis)
    {
        SetAsset? set = document.FindSetOrCut(setName);
        string[] parts = node.Split(SetCodec.PathSeparator);
        ObjectAsset? found = null;

        for (int i = 0; i < parts.Length && set is not null; i++)
        {
            found = set.Find(parts[i]);
            set = i < parts.Length - 1 && found?.Set is { Length: > 0 } inner ? document.FindSetOrCut(inner) : null;

            if (i < parts.Length - 1 && set is null)
            {
                found = null;
            }
        }

        if (found is null)
        {
            return channel is ChannelKind.Scale or ChannelKind.Alpha ? 1f : 0f;
        }

        return channel switch
        {
            ChannelKind.Position => axis == Axis.X ? found.X : axis == Axis.Y ? found.Y : 0f,
            ChannelKind.Scale => axis == Axis.X ? found.ScaleX : axis == Axis.Y ? found.ScaleY : 1f,
            ChannelKind.Euler => axis == Axis.Z ? found.Rotation : 0f,
            _ => 1f,
        };
    }

    private static ChannelAsset CloneChannel(ChannelAsset c)
    {
        var copy = new ChannelAsset { Object = c.Object, Kind = c.Kind };

        foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
        {
            if (c.Get(axis) is { } curve)
            {
                Set(copy, axis, new ComponentCurve
                {
                    Keys = curve.Keys.Select(k => new KeyAsset { T = k.T, V = k.V, InTangent = k.InTangent, OutTangent = k.OutTangent, Tangent = k.Tangent, Broken = k.Broken }).ToList(),
                });
            }
        }

        return copy;
    }

    private static void Set(ChannelAsset channel, Axis axis, ComponentCurve curve)
    {
        switch (axis)
        {
            case Axis.X: channel.X = curve; break;
            case Axis.Y: channel.Y = curve; break;
            default: channel.Z = curve; break;
        }
    }
}
