using System.Text.Json;
using Ked.Presentation.Core;

namespace Mt.Authoring.Sets;

/// <summary>
/// <c>presets/depth.json</c>(ExportedTuning) — 깊이의 유일한 진실인 level 커브. 레이어 라벨(far…close)은
/// 코어 <see cref="DepthLevelLabels"/>가 커브 위 눈금으로 바꾼다.
/// </summary>
public sealed class DepthTuningFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private DepthTuningFile(DepthLevelTuningDto level)
    {
        Level = level;
    }

    public DepthLevelTuningDto Level { get; }

    public static DepthTuningFile Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static DepthTuningFile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        DepthTuningFileDto dto = JsonSerializer.Deserialize<DepthTuningFileDto>(json, Options) ?? throw new JsonException("빈 depth.json이다.");
        DepthLevelTuningDto? level = dto.MonoBehaviour?.level;

        if (level is null || !level.TryResolve(DepthLevelLabels.Mid, out _, out _))
        {
            throw new JsonException("depth.json에 level 커브(yCurve·scaleCurve)가 없다.");
        }

        return new DepthTuningFile(level);
    }
}
