using Ked.Presentation.Core;
using Mt.Authoring.Model;

namespace Mt.Authoring.Sets;

/// <summary>
/// 문서가 정의한 계층(세트 · 컷)를 세운 한 벌로, 그리고 그 세트가 실제로 입는 스킨.
/// </summary>
public static class SetBuilder
{
    /// <summary>
    /// 문서가 정의한 계층(세트 · 컷)로 세운 한 벌 — 무대 크기는 <paramref name="stageSize"/>. 세울 수 없는 모양이면 예외 —
    /// 호출부(세션)가 빈 한 벌로 물러나고 사람에게 알린다.
    /// </summary>
    public static BuiltSets Compose(AuthoringDocument document, Vec2 stageSize)
    {
        ArgumentNullException.ThrowIfNull(document);
        // 컷은 오브젝트 계층에 값만 담는 점들이 더 선다(카메라 둘 · 맨 위 오브젝트마다 깊이 하나) — 카메라와 깊이가 같은 타임라인의 트랙이 된다.
        return BuiltSets.Of(stageSize, document.Sets.Select(r => (r.Name, SetCodec.Expand(r, document.FindSetOrCut)))
            .Concat(document.Cuts.Select(c => (c.Name, SetCodec.WithPoints(SetCodec.Expand(c.AsSet, document.FindSetOrCut), Preview.CutStage.PointsOf(c))))));
    }

    /// <summary>
    /// 그 세트가 실제로 입는 스킨 — 제 스킨(<paramref name="skinName"/>)의 그림에, 품은 세트들의 스킨을 경로 이름(<c>인스턴스.조각</c>)으로 더한 것.
    /// 품은 세트의 스킨은 인스턴스가 고른 것(없으면 그 세트의 첫 스킨)이다. 그릴 것이 하나도 없으면 null.
    /// </summary>
    public static SkinAsset? EffectiveSkin(AuthoringDocument document, string setName, string? skinName)
    {
        ArgumentNullException.ThrowIfNull(document);
        SkinAsset? own = document.FindSkin(skinName) is { } found && found.Set == setName ? found : null;
        SetAsset? set = document.FindSetOrCut(setName);

        if (set is null || set.Objects.All(n => string.IsNullOrEmpty(n.Set)))
        {
            return own; // 품은 것이 없다 — 제 스킨 그대로(사본을 만들지 않는다)
        }

        var merged = new SkinAsset { Name = own?.Name ?? "", Set = setName };

        if (own is not null)
        {
            foreach ((string node, string image) in own.Parts)
            {
                merged.Parts[node] = image;
            }

            foreach ((string node, SkinPartSetup setup) in own.Setups)
            {
                merged.Setups[node] = setup;
            }
        }

        Merge(document, set, "", merged, new HashSet<string>(StringComparer.Ordinal) { setName }, 0);
        return merged.Parts.Count == 0 && own is null ? null : merged;
    }

    private static void Merge(AuthoringDocument document, SetAsset set, string prefix, SkinAsset into, HashSet<string> open, int depth)
    {
        foreach (ObjectAsset node in set.Objects)
        {
            if (node.Set is not { Length: > 0 } inner || document.FindSetOrCut(inner) is not { } child || depth >= SetCodec.MaxNesting || !open.Add(inner))
            {
                continue;
            }

            string path = prefix + node.Id + SetCodec.PathSeparator;
            SkinAsset? skin = document.FindSkin(node.SetSkin) is { } chosen && chosen.Set == inner ? chosen : document.Skins.FirstOrDefault(s => s.Set == inner);

            if (skin is not null)
            {
                foreach ((string part, string image) in skin.Parts)
                {
                    into.Parts[path + part] = image;
                }

                foreach ((string part, SkinPartSetup setup) in skin.Setups)
                {
                    into.Setups[path + part] = setup;
                }
            }

            Merge(document, child, path, into, open, depth + 1);
            open.Remove(inner);
        }
    }
}
