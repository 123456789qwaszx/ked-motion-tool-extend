using Ked.Presentation.Core;
using Mt.Authoring.Model;

namespace Mt.Authoring.Editing;

// 세트를 가르고 이름을 바꾼다(PLAN-set.md — pillar를 조립으로 다시 지으며 드러난 불편). 한 덩어리로 지은 세트에서 몇 오브젝트를 따로 세트로 빼
// 조립으로 옮기는 길이다 — 자리·그림·키를 데리고 가고, 보이는 것은 그대로다.
public sealed partial class MotionEditor
{
    /// <summary>
    /// 고른 오브젝트들(과 그 밑)을 **새 세트로 뺀다** — 그 자리에는 새 세트의 인스턴스가 선다. 통째로 되돌리기 한 칸. 인스턴스의 이름을 돌려준다.
    /// <list type="bullet">
    /// <item>자리: 고른 것들의 가운데가 새 세트의 가운데가 되고, 인스턴스가 그 자리에 선다 — 화면에서 보이는 자리는 그대로다.</item>
    /// <item>그림: 그 오브젝트들의 스킨 칸이 새 세트의 스킨으로 옮겨진다.</item>
    /// <item>키: 그 오브젝트들의 채널(과 재생 블록)이 새 세트의 애니메이션(<c>새세트_이름</c>)으로 옮겨지고, 원래 애니메이션에는 그것을 처음부터 트는 재생 블록이 선다 — 재생은 그대로다.</item>
    /// <item>가리키던 것: 이 세트를 품은 다른 세트의 경로 채널, 연출의 붙는 자리, 씬 오브젝트의 붙는 노드가 새 경로(<c>인스턴스.이름</c>)를 따라온다.</item>
    /// </list>
    /// 고른 것들의 맨 위는 같은 부모 밑이어야 한다(자리를 한 기준으로 옮긴다).
    /// </summary>
    public string ExtractRigNodes(string kind, IReadOnlyCollection<string> ids, string newKind)
    {
        ArgumentNullException.ThrowIfNull(ids);
        RigAsset rig = RequireRig(kind);
        string name = RequireNewRigKind(newKind);

        // 고른 것 + 그 밑(부모가 앞에 서므로 한 번 훑으면 된다).
        var moved = new HashSet<string>(StringComparer.Ordinal);

        foreach (string id in ids)
        {
            RequireNodeIndex(rig, id);
            moved.Add(id);
        }

        foreach (RigNodeAsset node in rig.Nodes.Where(n => moved.Contains(n.Parent)))
        {
            moved.Add(node.Id);
        }

        if (moved.Count == 0)
        {
            throw new InvalidOperationException("뺄 오브젝트를 고르지 않았다.");
        }

        List<RigNodeAsset> roots = rig.Nodes.Where(n => moved.Contains(n.Id) && !moved.Contains(n.Parent)).ToList();
        string parent = roots[0].Parent;

        if (roots.Any(r => r.Parent != parent))
        {
            throw new InvalidOperationException("같은 부모 밑의 오브젝트들만 한 세트로 뺄 수 있다.");
        }

        // 가운데 — 맨 위 오브젝트들의 사각형을 감싸는 상자의 가운데(회전·배율은 치지 않는다).
        float cx = (roots.Min(r => r.X - r.Width / 2f) + roots.Max(r => r.X + r.Width / 2f)) / 2f;
        float cy = (roots.Min(r => r.Y - r.Height / 2f) + roots.Max(r => r.Y + r.Height / 2f)) / 2f;

        string instance = name;

        for (int i = 2; rig.Nodes.Any(n => n.Id == instance && !moved.Contains(n.Id)); i++)
        {
            instance = $"{name}_{i}";
        }

        bool Moves(string node) => moved.Contains(node) || moved.Contains(node.Split(RigAssetCodec.PathSeparator)[0]);

        Mutate(MotionChangeKind.Document, () =>
        {
            // 세트 — 고른 것들이 새 세트로, 그 자리에 인스턴스가.
            var inner = new RigAsset { Kind = name };
            int at = rig.Nodes.FindIndex(n => moved.Contains(n.Id));

            foreach (RigNodeAsset node in rig.Nodes.Where(n => moved.Contains(n.Id)).ToList())
            {
                rig.Nodes.Remove(node);
                inner.Nodes.Add(node);

                if (roots.Contains(node))
                {
                    node.Parent = "";
                    node.X -= cx;
                    node.Y -= cy;
                }
            }

            rig.Nodes.Insert(at, new RigNodeAsset { Id = instance, Parent = parent, X = cx, Y = cy, Set = name, Alpha = true });
            Document.Rigs.Insert(Document.Rigs.IndexOf(rig), inner); // 품긴 세트가 앞에 — 목록에서 "작은 것부터"

            // 그림 — 그 오브젝트들의 스킨 칸을 새 세트의 스킨으로.
            foreach (SkinAsset skin in Document.Skins.Where(s => s.Rig == kind).ToList())
            {
                List<string> parts = skin.Parts.Keys.Concat(skin.Setups.Keys).Where(moved.Contains).Distinct().ToList();

                if (parts.Count == 0)
                {
                    continue;
                }

                var copy = new SkinAsset { Name = UniqueName(Document.FindSkin(name) is null ? name : $"{name}_{skin.Name}", n => Document.FindSkin(n) is not null), Rig = name };

                foreach (string part in parts)
                {
                    if (skin.Parts.Remove(part, out string? image))
                    {
                        copy.Parts[part] = image;
                    }

                    if (skin.Setups.Remove(part, out SkinPartSetup? setup))
                    {
                        copy.Setups[part] = setup;
                    }
                }

                Document.Skins.Add(copy);
            }

            // 키 — 그 오브젝트들의 채널·재생 블록을 새 세트의 애니메이션으로, 원래 자리에는 그것을 트는 재생 블록을.
            foreach (MotionAsset motion in Document.Motions.Where(m => m.Rig == kind).ToList())
            {
                List<ChannelAsset> channels = motion.Channels.Where(c => Moves(c.Node)).ToList();
                List<MotionPlayAsset> plays = motion.Plays.Where(p => moved.Contains(p.Node)).ToList();

                if (channels.Count == 0 && plays.Count == 0)
                {
                    continue;
                }

                string suffix = motion.Name.StartsWith(kind + "_", StringComparison.Ordinal) ? motion.Name[(kind.Length + 1)..] : motion.Name;
                var part = new MotionAsset
                {
                    Name = UniqueName($"{name}_{suffix}", n => Document.Find(n) is not null),
                    Rig = name,
                    Duration = motion.Duration,
                    End = motion.End,
                    PreviewMode = motion.PreviewMode,
                    Channels = channels,
                    Plays = plays,
                };
                motion.Channels.RemoveAll(channels.Contains);
                motion.Plays.RemoveAll(plays.Contains);

                // 절대값 애니메이션의 자리 키는 "세트 안의 자리"다 — 맨 위 오브젝트는 가운데만큼 옮겨졌다.
                if (motion.PreviewMode == MotionChannelMode.Absolute)
                {
                    foreach (ChannelAsset channel in channels.Where(c => c.Kind == MotionChannelKind.Position && roots.Any(r => r.Id == c.Node)))
                    {
                        channel.X?.Keys.ForEach(k => k.V -= cx);
                        channel.Y?.Keys.ForEach(k => k.V -= cy);
                    }
                }

                Document.Motions.Insert(Document.Motions.IndexOf(motion), part);
                motion.Plays.Insert(0, new MotionPlayAsset { Node = instance, Motion = part.Name, At = 0f, Repeat = 1 });
            }

            // 가리키던 것 — 이 세트를 품은 다른 세트의 경로 채널(a.b.이름 → a.b.인스턴스.이름).
            foreach (MotionAsset motion in Document.Motions.Where(m => m.Rig != kind && m.Rig != name))
            {
                foreach (ChannelAsset channel in motion.Channels)
                {
                    channel.Node = ThroughInstance(motion.Rig, channel.Node, kind, moved, instance);
                }
            }

            foreach (EmojiAsset emoji in Document.Emojis.Where(e => e.Rig == kind && Moves(e.AnchorNode)))
            {
                emoji.AnchorNode = instance + RigAssetCodec.PathSeparator + emoji.AnchorNode;
            }

            foreach (SceneAsset scene in Document.Scenes)
            {
                foreach (SceneObject o in scene.Objects.Where(o => o.AttachNode is { } attach && Moves(attach) && scene.ParentOf(o)?.Rig == kind))
                {
                    o.AttachNode = instance + RigAssetCodec.PathSeparator + o.AttachNode;
                }
            }
        });

        return instance;
    }

    /// <summary>
    /// 세트의 이름을 바꾼다 — 그 이름을 가리키던 것이 같은 칸에서 따라온다: 그 세트의 애니메이션·스킨·연출, 그것을 품은 인스턴스, 씬의 오브젝트.
    /// </summary>
    public void RenameRig(string kind, string newKind)
    {
        RigAsset rig = RequireRig(kind);

        if (string.Equals(kind, newKind?.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        string name = RequireNewRigKind(newKind);

        Mutate(MotionChangeKind.Document, () =>
        {
            rig.Kind = name;

            foreach (MotionAsset motion in Document.Motions.Where(m => m.Rig == kind))
            {
                motion.Rig = name;
            }

            foreach (SkinAsset skin in Document.Skins.Where(s => s.Rig == kind))
            {
                skin.Rig = name;
            }

            foreach (EmojiAsset emoji in Document.Emojis.Where(e => e.Rig == kind))
            {
                emoji.Rig = name;
            }

            foreach (RigNodeAsset node in Document.Rigs.SelectMany(r => r.Nodes).Where(n => n.Set == kind))
            {
                node.Set = name;
            }

            foreach (SceneObject o in Document.Scenes.SelectMany(s => s.Objects).Where(o => o.Rig == kind))
            {
                o.Rig = name;
            }
        });
    }

    private string RequireNewRigKind(string? kind)
    {
        string name = (kind ?? "").Trim();

        if (!RigKindRule.IsMatch(name))
        {
            throw new InvalidOperationException($"세트 이름은 영문자로 시작하는 영문·숫자·_ 다 ('{kind}')");
        }

        if (ReservedRigKinds.Contains(name))
        {
            throw new InvalidOperationException($"'{name}'는 툴에서 정의하지 않는 리그다.");
        }

        if (Document.FindRig(name) is not null)
        {
            throw new InvalidOperationException($"세트 '{name}'가 이미 있다.");
        }

        return name;
    }

    private static string UniqueName(string stem, Func<string, bool> taken)
    {
        string name = stem;

        for (int i = 2; taken(name); i++)
        {
            name = $"{stem}_{i}";
        }

        return name;
    }

    /// <summary>경로 이름이 <paramref name="kind"/> 세트의 옮겨진 오브젝트를 지나가면, 그 앞에 인스턴스를 끼운다.</summary>
    private string ThroughInstance(string rigKind, string path, string kind, HashSet<string> moved, string instance)
    {
        string[] parts = path.Split(RigAssetCodec.PathSeparator);
        RigAsset? rig = Document.FindRig(rigKind);

        for (int i = 0; i + 1 < parts.Length && rig is not null; i++)
        {
            rig = rig.Find(parts[i])?.Set is { Length: > 0 } inner ? Document.FindRig(inner) : null;

            if (rig?.Kind == kind)
            {
                return moved.Contains(parts[i + 1])
                    ? string.Join(RigAssetCodec.PathSeparator, parts.Take(i + 1).Append(instance).Concat(parts.Skip(i + 1)))
                    : path;
            }
        }

        return path;
    }
}
