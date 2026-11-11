using System.Text.RegularExpressions;
using Mt.Authoring.Model;

namespace Mt.Authoring.Editing;

// 툴에서 정의하는 세트의 명령(PLAN-parts.md §9) — 각각 되돌리기 한 칸.
public sealed partial class DocumentEditor
{
    private static readonly Regex SetNameRule = new("^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    /// <summary>세트를 문서에 더한다. 노드는 사본으로 들어간다. id가 겹치거나 부모가 앞에 없으면 막는다(세울 수 없는 세트다).</summary>
    public SetAsset AddSet(SetAsset set)
    {
        ArgumentNullException.ThrowIfNull(set);

        if (!SetNameRule.IsMatch(set.Name ?? ""))
        {
            throw new InvalidOperationException($"세트 이름은 영문자로 시작하는 영문·숫자·_ 다 ('{set.Name}')");
        }

        if (Document.FindSetOrCut(set.Name) is not null)
        {
            throw new InvalidOperationException($"세트 '{set.Name}'가 이미 있다.");
        }

        SetAsset copy = CloneSet(set);
        RequireBuildable(copy);
        Mutate(DocumentChangeKind.Document, () => Document.Sets.Add(copy));
        return copy;
    }

    /// <summary>세트를 지운다. 그 세트의 타임라인·스킨은 남는다 — 검증이 "없는 세트"로 알린다.</summary>
    public void RemoveSet(string kind)
    {
        SetAsset set = RequireSet(kind);

        // 다른 세트가 품고 있으면 못 지운다 — 참조가 허공을 가리키게 두지 않는다.
        if (Document.AllSetsAndCuts.FirstOrDefault(r => r.Objects.Any(n => n.Set == kind)) is { } holder)
        {
            throw new InvalidOperationException($"세트 '{kind}'를 '{holder.Name}'가 품고 있다 — 먼저 거기서 뺀다.");
        }

        Mutate(DocumentChangeKind.Document, () => Document.Sets.Remove(set));
    }

    /// <summary>
    /// 다른 세트를 품는다(<c>PLAN-set.md</c> S9) — 그 세트의 인스턴스 오브젝트를 더한다(참조: 그 세트를 고치면 여기도 바뀐다).
    /// 자기 자신이나, 자기를 (몇 다리 건너서라도) 품고 있는 세트는 못 품는다 — 끝없이 돈다.
    /// </summary>
    public ObjectAsset AddInstance(string kind, string id, string setKind, string? parent = null)
    {
        ArgumentNullException.ThrowIfNull(setKind);
        RequireSetOrCut(kind);

        if (Document.FindSetOrCut(setKind) is null)
        {
            throw new KeyNotFoundException($"세트 '{setKind}'가 문서에 없다.");
        }

        if (Document.FindCut(setKind) is not null)
        {
            throw new InvalidOperationException($"'{setKind}'는 컷이다 — 컷은 품을 수 없다(묶어서 다시 쓸 것은 세트로 뺀다).");
        }

        if (Holds(setKind, kind, new HashSet<string>(StringComparer.Ordinal)))
        {
            throw new InvalidOperationException($"세트 '{setKind}'는 '{kind}'를 품고 있다(또는 자기 자신이다) — 서로 품으면 끝없이 돈다.");
        }

        ObjectAsset node = null!;
        Batch(DocumentChangeKind.Document, () =>
        {
            node = AddObject(kind, id, parent);
            Mutate(DocumentChangeKind.Document, () =>
            {
                node.Set = setKind;
                node.Alpha = true; // 품은 것을 통째로 흐리게 할 수 있게
            });
        });
        return node;
    }

    /// <summary>인스턴스가 품은 세트에 입힐 스킨을 고른다. null이면 그 세트의 첫 스킨.</summary>
    public void SetInstanceSkin(string kind, string id, string? skin)
    {
        SetAsset set = RequireSetOrCut(kind);
        ObjectAsset node = set.Objects[RequireNodeIndex(set, id)];

        if (string.IsNullOrEmpty(node.Set))
        {
            throw new InvalidOperationException($"'{id}'는 세트를 품은 오브젝트가 아니다.");
        }

        string? name = string.IsNullOrWhiteSpace(skin) ? null : skin.Trim();

        if (name is not null && Document.FindSkin(name)?.Set != node.Set)
        {
            throw new InvalidOperationException($"스킨 '{name}'은 세트 '{node.Set}'의 것이 아니다.");
        }

        if (node.SetSkin != name)
        {
            Mutate(DocumentChangeKind.Document, () => node.SetSkin = name);
        }
    }

    /// <summary><paramref name="kind"/>가 <paramref name="target"/>이거나 그것을 (몇 다리 건너) 품고 있는가.</summary>
    private bool Holds(string kind, string target, HashSet<string> seen)
        => kind == target
           || (seen.Add(kind) && Document.FindSetOrCut(kind) is { } set && set.Objects.Any(n => n.Set is { Length: > 0 } inner && Holds(inner, target, seen)));

    // ── 세트 편집(PLAN-parts.md §12) — 노드를 하나씩 짓고 옮긴다 ────────

    /// <summary>
    /// 노드를 더한다 — <paramref name="parent"/>(비면 루트) 밑의 맨 뒤(그 부모의 식구들 뒤 = 가장 위에 그려진다). id는 그 세트에서 하나뿐이어야 한다.
    /// </summary>
    public ObjectAsset AddObject(string kind, string id, string? parent = null, bool image = false)
    {
        SetAsset set = RequireSetOrCut(kind);
        string name = RequireNodeId(set, id);
        string under = parent ?? "";

        if (under.Length > 0 && set.Find(under) is null)
        {
            throw new KeyNotFoundException($"부모 오브젝트 '{under}'가 세트 '{kind}'에 없다.");
        }

        var node = new ObjectAsset { Id = name, Parent = under, Image = image, Alpha = image, Width = image ? ObjectAsset.DefaultImageSize : 0f, Height = image ? ObjectAsset.DefaultImageSize : 0f };
        int at = under.Length == 0 ? set.Objects.Count : SubtreeEnd(set, set.Objects.FindIndex(n => n.Id == under));
        Mutate(DocumentChangeKind.Document, () => set.Objects.Insert(at, node));
        return node;
    }

    /// <summary>노드를 지운다 — 그 밑의 노드도 함께. 그 노드를 쓰던 채널·스킨 칸은 남고 검증이 알린다(되살리면 다시 맞는다).</summary>
    public void RemoveObject(string kind, string id)
    {
        SetAsset set = RequireSetOrCut(kind);
        int index = RequireNodeIndex(set, id);
        int end = SubtreeEnd(set, index);
        Mutate(DocumentChangeKind.Document, () => set.Objects.RemoveRange(index, end - index));
    }

    /// <summary>
    /// 노드의 이름을 바꾼다 — 그 이름을 가리키던 것이 같은 칸에서 따라온다: 자식의 부모, 그 세트의 타임라인 채널, 스킨의 조각 칸.
    /// </summary>
    public void RenameObject(string kind, string id, string newId)
    {
        SetAsset set = RequireSetOrCut(kind);
        ObjectAsset node = set.Objects[RequireNodeIndex(set, id)];

        if (string.Equals(id, newId?.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        string name = RequireNodeId(set, newId);

        // 그 세트의 타임라인에 새 이름의 채널이 이미 있으면(지운 노드의 흔적) 합칠 수 없다.
        if (Document.Timelines.Any(m => m.Set == kind && m.Channels.Any(c => c.Object == name)))
        {
            throw new InvalidOperationException($"세트 '{kind}'의 타임라인에 오브젝트 '{name}'의 채널이 이미 있다 — 먼저 그 채널을 지운다.");
        }

        Mutate(DocumentChangeKind.Document, () =>
        {
            node.Id = name;

            foreach (ObjectAsset child in set.Objects.Where(n => n.Parent == id))
            {
                child.Parent = name;
            }

            foreach (ChannelAsset channel in Document.Timelines.Where(m => m.Set == kind).SelectMany(m => m.Channels).Where(c => c.Object == id))
            {
                channel.Object = name;
            }

            // 컷의 맨 위 오브젝트면 깊이의 트랙(이름__depth)도 따라온다.
            foreach (ChannelAsset channel in Document.Timelines.Where(m => m.Set == kind).SelectMany(m => m.Channels).Where(c => c.Object == Preview.CutStage.DepthNodeOf(id)))
            {
                channel.Object = Preview.CutStage.DepthNodeOf(name);
            }

            // 세트를 품은 오브젝트(인스턴스)면 — 그 안쪽을 짚던 경로 채널(이름.안쪽)과 그것을 트는 재생 블록도 따라온다.
            string prefix = id + SetCodec.PathSeparator;

            foreach (TimelineAsset timeline in Document.Timelines.Where(m => m.Set == kind))
            {
                foreach (ChannelAsset channel in timeline.Channels.Where(c => c.Object.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    channel.Object = name + channel.Object[id.Length..];
                }

                foreach (PlayBlockAsset play in timeline.Plays.Where(p => p.Object == id))
                {
                    play.Object = name;
                }

                // 그림 키도 따라온다(그 오브젝트의 것 · 인스턴스 안쪽의 것).
                foreach (ImageKeyAsset imageKey in timeline.ImageKeys.Where(s => s.Object == id || s.Object.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    imageKey.Object = name + imageKey.Object[id.Length..];
                }
            }

            foreach (SkinAsset skin in Document.Skins.Where(s => s.Set == kind))
            {
                if (skin.Parts.Remove(id, out string? image))
                {
                    skin.Parts[name] = image;
                }

                if (skin.Setups.Remove(id, out SkinPartSetup? setup))
                {
                    skin.Setups[name] = setup;
                }
            }
        });
    }

    /// <summary>노드의 값(위치 · 크기 · 그림을 그리는가 · 불투명도를 받는가)을 고친다. id와 부모는 따로(<see cref="RenameObject"/> · <see cref="SetObjectParent"/>).</summary>
    public void UpdateObject(string kind, string id, ObjectAsset value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetAsset set = RequireSetOrCut(kind);
        ObjectAsset node = set.Objects[RequireNodeIndex(set, id)];

        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Width) || !float.IsFinite(value.Height) || value.Width < 0f || value.Height < 0f)
        {
            throw new InvalidOperationException("오브젝트의 위치는 수, 크기는 0 이상의 수다.");
        }

        if (!float.IsFinite(value.Rotation) || !float.IsFinite(value.ScaleX) || !float.IsFinite(value.ScaleY) || value.ScaleX == 0f || value.ScaleY == 0f)
        {
            throw new InvalidOperationException("오브젝트의 회전은 수, 배율은 0이 아닌 수다.");
        }

        string? color = NormalizeColor(value.Color);

        if (node.X == value.X && node.Y == value.Y && node.Width == value.Width && node.Height == value.Height && node.Image == value.Image && node.Alpha == value.Alpha
            && node.Rotation == value.Rotation && node.ScaleX == value.ScaleX && node.ScaleY == value.ScaleY && node.Color == color)
        {
            return;
        }

        Mutate(DocumentChangeKind.Document, () =>
        {
            node.X = value.X;
            node.Y = value.Y;
            node.Width = value.Width;
            node.Height = value.Height;
            node.Image = value.Image;
            node.Alpha = value.Alpha;
            node.Rotation = value.Rotation;
            node.ScaleX = value.ScaleX;
            node.ScaleY = value.ScaleY;
            node.Color = color;
        });
    }

    private static readonly Regex ColorRule = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);

    /// <summary>색 글을 고른 꼴로 — "#RRGGBB" 또는 "#RRGGBBAA"(대문자). 비면 null(색 오브젝트가 아니다). 꼴이 아니면 막는다.</summary>
    public static string? NormalizeColor(string? color)
    {
        string text = (color ?? "").Trim();

        if (text.Length == 0)
        {
            return null;
        }

        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        return ColorRule.IsMatch(text) ? text.ToUpperInvariant() : throw new InvalidOperationException($"색은 #RRGGBB 또는 #RRGGBBAA다 ('{color}')");
    }

    /// <summary>노드를 다른 부모 밑으로 옮긴다(비면 루트) — 밑의 노드도 함께, 새 부모의 식구들 맨 뒤로. 제 밑으로는 못 옮긴다.</summary>
    public void SetObjectParent(string kind, string id, string? parent)
    {
        SetAsset set = RequireSetOrCut(kind);
        int index = RequireNodeIndex(set, id);
        string under = parent ?? "";

        if (set.Objects[index].Parent == under)
        {
            return;
        }

        int end = SubtreeEnd(set, index);
        List<ObjectAsset> subtree = set.Objects.GetRange(index, end - index);

        if (under.Length > 0 && set.Find(under) is null)
        {
            throw new KeyNotFoundException($"부모 오브젝트 '{under}'가 세트 '{kind}'에 없다.");
        }

        if (subtree.Any(n => n.Id == under))
        {
            throw new InvalidOperationException($"오브젝트 '{id}'를 제 밑('{under}')으로 옮길 수 없다.");
        }

        Mutate(DocumentChangeKind.Document, () =>
        {
            set.Objects.RemoveRange(index, subtree.Count);
            subtree[0].Parent = under;
            int at = under.Length == 0 ? set.Objects.Count : SubtreeEnd(set, set.Objects.FindIndex(n => n.Id == under));
            set.Objects.InsertRange(at, subtree);
        });
    }

    /// <summary>형제 사이에서 순서를 옮긴다(−1 = 앞으로 = 아래에 그려진다, +1 = 뒤로 = 위에) — 밑의 노드도 함께. 끝이면 아무 일도 없다.</summary>
    public void MoveObject(string kind, string id, int direction)
    {
        SetAsset set = RequireSetOrCut(kind);
        int index = RequireNodeIndex(set, id);
        string parent = set.Objects[index].Parent;
        List<int> siblings = Enumerable.Range(0, set.Objects.Count).Where(i => set.Objects[i].Parent == parent).ToList();
        int place = siblings.IndexOf(index) + Math.Sign(direction);

        if (direction == 0 || place < 0 || place >= siblings.Count)
        {
            return;
        }

        // 앞쪽 식구와 뒤쪽 식구(각각 밑의 노드까지)를 맞바꾼다 — 형제는 이어져 있다.
        int first = Math.Min(index, siblings[place]);
        int second = Math.Max(index, siblings[place]);
        int secondEnd = SubtreeEnd(set, second);
        List<ObjectAsset> front = set.Objects.GetRange(first, second - first);
        List<ObjectAsset> back = set.Objects.GetRange(second, secondEnd - second);

        Mutate(DocumentChangeKind.Document, () =>
        {
            set.Objects.RemoveRange(first, secondEnd - first);
            set.Objects.InsertRange(first, back.Concat(front));
        });
    }

    /// <summary>그 노드의 식구(밑의 노드 전부)가 끝나는 자리 — 부모가 자식보다 먼저 오므로 식구는 이어져 있다.</summary>
    private static int SubtreeEnd(SetAsset set, int index)
    {
        var family = new HashSet<string>(StringComparer.Ordinal) { set.Objects[index].Id };
        int end = index + 1;

        while (end < set.Objects.Count && family.Contains(set.Objects[end].Parent))
        {
            family.Add(set.Objects[end].Id);
            end++;
        }

        return end;
    }

    private static int RequireNodeIndex(SetAsset set, string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        int index = set.Objects.FindIndex(n => string.Equals(n.Id, id, StringComparison.Ordinal));
        return index >= 0 ? index : throw new KeyNotFoundException($"오브젝트 '{id}'가 세트 '{set.Name}'에 없다.");
    }

    private static readonly Regex NodeIdRule = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static string RequireNodeId(SetAsset set, string? id)
    {
        string name = (id ?? "").Trim();

        if (!NodeIdRule.IsMatch(name))
        {
            throw new InvalidOperationException($"오브젝트 이름은 영문자로 시작하는 영문·숫자·_ 다 ('{id}')");
        }

        if (set.Find(name) is not null)
        {
            throw new InvalidOperationException($"오브젝트 '{name}'가 세트 '{set.Name}'에 이미 있다.");
        }

        if (Preview.CutStage.IsPointNode(name))
        {
            throw new InvalidOperationException($"'{name}'는 카메라·깊이의 트랙이 쓰는 이름이다 — 다른 이름을 쓴다.");
        }

        return name;
    }

    private static void RequireBuildable(SetAsset set)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { SetCodec.RootId };

        foreach (ObjectAsset node in set.Objects)
        {
            if (string.IsNullOrEmpty(node.Id) || node.Id.Contains('/', StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"오브젝트 id가 비었거나 '/'가 들었다 ('{node.Id}')");
            }

            if (!string.IsNullOrEmpty(node.Parent) && !seen.Contains(node.Parent))
            {
                throw new InvalidOperationException($"오브젝트 '{node.Id}'의 부모 '{node.Parent}'가 앞에 없다 — 부모가 자식보다 먼저 와야 한다.");
            }

            if (!seen.Add(node.Id))
            {
                throw new InvalidOperationException($"오브젝트 '{node.Id}'가 둘이다.");
            }
        }
    }

    private static SetAsset CloneSet(SetAsset set) => new()
    {
        Name = set.Name,
        Objects = set.Objects.Select(n =>
        {
            ObjectAsset copy = n.Clone();
            copy.Parent = n.Parent ?? "";
            copy.Color = NormalizeColor(n.Color);
            return copy;
        }).ToList(),
    };

    /// <summary>세트만 — 컷의 오브젝트 계층은 세트의 목록에 없다(만들기·지우기·이름은 컷의 명령으로).</summary>
    private SetAsset RequireSet(string kind)
    {
        SetAsset set = RequireSetOrCut(kind);

        if (Document.FindCut(kind) is not null)
        {
            throw new InvalidOperationException($"'{kind}'는 컷이다 — 컷의 명령으로 고친다.");
        }

        return set;
    }

    private SetAsset RequireSetOrCut(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return Document.FindSetOrCut(kind) ?? throw new KeyNotFoundException($"세트 '{kind}'가 문서에 없다.");
    }
}
