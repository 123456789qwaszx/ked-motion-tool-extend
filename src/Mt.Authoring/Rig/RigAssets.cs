using System.Text.Encodings.Web;
using System.Text.Json;
using Ked.Presentation.Core;
using Mt.Authoring.Model;

namespace Mt.Authoring.Rig;

/// <summary>
/// 문서가 정의한 리그를 덤프와 한 벌로(<c>PLAN-parts.md</c> §9), 그리고 런타임이 읽을 파일로.
/// </summary>
public static class RigAssets
{
    /// <summary>
    /// 덤프의 리그 + 문서의 리그. 같은 rigKind면 문서의 것이 이긴다. 문서의 리그가 세울 수 없는 모양이면 예외 —
    /// 호출부(세션)가 덤프만으로 물러나고 사람에게 알린다.
    /// </summary>
    public static RigSchemaFile Compose(RigSchemaFile dump, MotionDocument document)
    {
        ArgumentNullException.ThrowIfNull(dump);
        ArgumentNullException.ThrowIfNull(document);
        Vec2 stage = dump.Dto.capturedUnderParentSize.ToVec2();
        // 컷은 오브젝트 계층에 카메라의 점 둘이 더 선다 — 카메라가 같은 타임라인의 트랙이 된다(그 점의 자리 = 밀기·당기기의 값).
        return dump.With(document.Rigs.Select(r => RigAssetCodec.ToDto(r, stage, document.FindRig))
            .Concat(document.Cuts.Select(c => RigAssetCodec.WithPoints(RigAssetCodec.ToDto(c.Rig, stage, document.FindRig), Preview.CutStage.CameraPan, Preview.CutStage.CameraZoom))));
    }

    /// <summary>
    /// 그 세트가 실제로 입는 스킨 — 제 스킨(<paramref name="skinName"/>)의 그림에, 품은 세트들의 스킨을 경로 이름(<c>인스턴스.조각</c>)으로 더한 것.
    /// 품은 세트의 스킨은 인스턴스가 고른 것(없으면 그 세트의 첫 스킨)이다. 그릴 것이 하나도 없으면 null.
    /// </summary>
    public static SkinAsset? EffectiveSkin(MotionDocument document, string kind, string? skinName)
    {
        ArgumentNullException.ThrowIfNull(document);
        SkinAsset? own = document.FindSkin(skinName) is { } found && found.Rig == kind ? found : null;
        RigAsset? rig = document.FindRig(kind);

        if (rig is null || rig.Nodes.All(n => string.IsNullOrEmpty(n.Set)))
        {
            return own; // 품은 것이 없다 — 제 스킨 그대로(사본을 만들지 않는다)
        }

        var merged = new SkinAsset { Name = own?.Name ?? "", Rig = kind };

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

        Merge(document, rig, "", merged, new HashSet<string>(StringComparer.Ordinal) { kind }, 0);
        return merged.Parts.Count == 0 && own is null ? null : merged;
    }

    private static void Merge(MotionDocument document, RigAsset rig, string prefix, SkinAsset into, HashSet<string> open, int depth)
    {
        foreach (RigNodeAsset node in rig.Nodes)
        {
            if (node.Set is not { Length: > 0 } inner || document.FindRig(inner) is not { } child || depth >= RigAssetCodec.MaxNesting || !open.Add(inner))
            {
                continue;
            }

            string path = prefix + node.Id + RigAssetCodec.PathSeparator;
            SkinAsset? skin = document.FindSkin(node.SetSkin) is { } chosen && chosen.Rig == inner ? chosen : document.Skins.FirstOrDefault(s => s.Rig == inner);

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

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 문서가 정의한 리그들을 **덤프와 같은 모양**(<c>rig-schemas.json</c>)으로 — 런타임은 캐릭터 리그를 읽는 그 로더로 읽어 세운다.
    /// 덤프의 리그는 싣지 않는다(저쪽이 원본이다).
    /// </summary>
    public static string WriteSchemas(MotionDocument document, Vec2 stageSize)
    {
        ArgumentNullException.ThrowIfNull(document);
        var dto = new RigSchemasFileDto
        {
            capturedUnderParentSize = new Float2Dto { x = stageSize.X, y = stageSize.Y },
            rigs = document.Rigs.OrderBy(r => r.Kind, StringComparer.Ordinal).Select(r => RigAssetCodec.ToDto(r, stageSize, document.FindRig)).ToList(),
        };
        return JsonSerializer.Serialize(dto, WriteOptions) + "\n";
    }
}
