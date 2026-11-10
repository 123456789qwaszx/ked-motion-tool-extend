using System.Text.Encodings.Web;
using System.Text.Json;
using Ked.Presentation.Core;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;

namespace Mt.Authoring.Serialization;

/// <summary>
/// 컷 파일(<c>cut/1</c> — <c>PLAN-standalone.md</c> §7) — 컷 하나를 **다 펼쳐 구워** 한 덩어리로 낸다. 툴이 형태를 정하고 게임이 읽는다.
/// 묶음(세트)은 오브젝트로 펼치고(<c>인스턴스.이름</c>), 재생 블록은 키로 굽고, 카메라와 깊이의 트랙은 제 자리로 옮기고, 시각은 프레임으로 적는다.
/// 읽는 쪽의 모양과 계산은 코어(<see cref="CutFileDto"/> · <see cref="CutPlayer"/>)에 있다 — 게임이 그것으로 재생한다.
/// 저작본(툴의 문서)은 따로다: 거기에는 묶음의 참조와 재생 블록이 살아 있다.
/// </summary>
public static class CutFileJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>컷을 파일의 모양으로 — 펼치고 굽는다.</summary>
    /// <param name="rebaseImage">문서 기준의 그림 경로를 컷 파일 기준으로 바꾼다. 없으면 그대로.</param>
    public static CutFileDto Build(MotionDocument document, CutAsset cut, Vec2 stageSize, Func<string, string>? rebaseImage = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cut);
        string Image(string path) => rebaseImage is null ? path : rebaseImage(path);
        float fps = CutAsset.Fps;

        var file = new CutFileDto
        {
            format = CutPlayer.Format,
            name = cut.Name,
            frames = cut.Frames,
            fps = CutAsset.Fps,
            stage = new CutStageDto { width = stageSize.X, height = stageSize.Y },
            camera = new CutCameraDto { panX = cut.Camera.PanX, panY = cut.Camera.PanY, zoom = cut.Camera.Zoom },
            objects = new List<CutObjectDto>(),
            tracks = new List<CutTrackDto>(),
            images = new List<CutImageTrackDto>(),
            events = new List<CutEventDto>(),
        };

        // 오브젝트 — 품은 세트를 펼친 것. 그림은 실제로 입는 스킨(제 스킨 + 품은 세트들의 스킨)에서.
        SkinAsset? skin = RigAssets.EffectiveSkin(document, cut.Name, document.Skins.FirstOrDefault(s => s.Rig == cut.Name)?.Name);

        foreach (RigSchemaNodeDto node in RigAssetCodec.ToDto(cut.Rig, stageSize, document.FindRig).nodes.Where(n => n.id != RigAssetCodec.RootId))
        {
            bool top = node.parent == RigAssetCodec.RootId;
            file.objects.Add(new CutObjectDto
            {
                id = node.id,
                parent = top ? "" : node.parent,
                depth = top ? cut.Rig.Find(node.id)?.Depth ?? CutMath.DefaultDepth : 0f,
                x = node.anchoredPosition.x,
                y = node.anchoredPosition.y,
                rotation = node.localEulerAngles.z,
                scaleX = node.localScale.x,
                scaleY = node.localScale.y,
                width = node.sizeDelta.x,
                height = node.sizeDelta.y,
                image = node.hasImage && skin?.ImageAt(node.id, 0) is { } image ? Image(image) : null,
                color = string.IsNullOrEmpty(node.fillColor) ? null : node.fillColor,
            });
        }

        // 타임라인 — 재생 블록을 구운 것. 채널마다 성분별 트랙으로, 카메라와 깊이의 점은 제 자리로.
        MotionAsset? timeline = document.Find(cut.Name);
        MotionAsset? baked = timeline is null ? null : timeline.Flattened ?? timeline;

        foreach (ChannelAsset channel in baked?.Channels ?? new List<ChannelAsset>())
        {
            foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
            {
                if (channel.Get(axis) is not { Keys.Count: > 0 } curve || TrackOf(channel, axis) is not { } track)
                {
                    continue;
                }

                file.tracks.Add(new CutTrackDto
                {
                    target = track.Target,
                    what = track.What,
                    keys = curve.Keys.Select(k => k.ToCurveKey()).Select(k => new CutKeyDto
                    {
                        f = Frame(k.Time * fps),
                        v = k.Value,
                        i = k.InTangent / fps, // 초당 → 프레임당
                        o = k.OutTangent / fps,
                    }).ToList(),
                });
            }
        }

        foreach (IGrouping<string, MotionSwapAsset> swaps in (baked?.Swaps ?? new List<MotionSwapAsset>()).GroupBy(s => s.Node))
        {
            file.images.Add(new CutImageTrackDto
            {
                target = swaps.Key,
                keys = swaps.OrderBy(s => s.T).Select(s => new CutImageKeyDto { f = Frame(s.T * fps), v = Image(s.Image) }).ToList(),
            });
        }

        foreach (MotionEventAsset e in baked?.Events ?? new List<MotionEventAsset>())
        {
            file.events.Add(new CutEventDto { f = Frame(e.T * fps), name = e.Name, value = e.Value });
        }

        return file;
    }

    /// <summary>프레임 — 프레임에 놓인 키는 정수로 떨어지게(0.5초 × 24 = 12.000001 같은 잡음을 걷는다). 프레임 사이의 키는 그대로 둔다.</summary>
    private static float Frame(float frames) => MathF.Abs(frames - MathF.Round(frames)) < 1e-3f ? MathF.Round(frames) : frames;

    /// <summary>채널의 성분이 컷 파일의 어느 트랙인가 — 카메라·깊이의 점은 제 자리로. 실리지 않는 성분이면 null.</summary>
    private static (string Target, string What)? TrackOf(ChannelAsset channel, Axis axis)
    {
        if (channel.Node == CutStage.CameraPan)
        {
            return channel.Kind == MotionChannelKind.Position && axis != Axis.Z ? (CutPlayer.CameraTarget, axis == Axis.X ? "panX" : "panY") : null;
        }

        if (channel.Node == CutStage.CameraZoom)
        {
            return channel.Kind == MotionChannelKind.Position && axis == Axis.X ? (CutPlayer.CameraTarget, "zoom") : null;
        }

        if (CutStage.IsDepthNode(channel.Node))
        {
            return channel.Kind == MotionChannelKind.Position && axis == Axis.X ? (channel.Node[..^CutStage.DepthSuffix.Length], "depth") : null;
        }

        string? what = (channel.Kind, axis) switch
        {
            (MotionChannelKind.Position, Axis.X) => "x",
            (MotionChannelKind.Position, Axis.Y) => "y",
            (MotionChannelKind.Scale, Axis.X) => "scaleX",
            (MotionChannelKind.Scale, Axis.Y) => "scaleY",
            (MotionChannelKind.Euler, Axis.Z) => "rotation",
            (MotionChannelKind.Alpha, Axis.X) => "alpha",
            _ => null,
        };
        return what is null ? null : (channel.Node, what);
    }

    public static string Write(CutFileDto file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return JsonSerializer.Serialize(file, Options) + "\n";
    }

    /// <summary>컷 파일을 읽는다 — 게임이 읽는 모양 그대로(코어의 DTO). 모르는 형식이면 예외.</summary>
    public static CutFileDto Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        CutFileDto file = JsonSerializer.Deserialize<CutFileDto>(json, Options) ?? throw new JsonException("컷 파일이 비었다.");

        if (file.format != CutPlayer.Format)
        {
            throw new JsonException($"모르는 컷 형식이다 ('{file.format}') — {CutPlayer.Format}을 읽는다.");
        }

        return file;
    }
}
