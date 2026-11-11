using Mt.Authoring.Model;

namespace Mt.Authoring.Preview;

/// <summary>그림 키를 읽는다 — 계단 키: 그 시각에 걸린 것은 "그 시각 이전의 마지막 키"다(사이값이 없다).</summary>
public static class ImageKeys
{
    private const double Epsilon = 1e-6;

    /// <summary>그 시각에 그 오브젝트가 그릴 그림 — 그림 키가 아직 없으면 null(스킨의 그림 그대로). 품은 애니메이션(재생 블록)의 것까지 본다.</summary>
    public static string? ImageAt(TimelineAsset timeline, string node, double seconds)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        string? image = null;

        foreach (ImageKeyAsset imageKey in (timeline.Flattened ?? timeline).ImageKeys)
        {
            if (imageKey.T > seconds + Epsilon)
            {
                break;
            }

            if (imageKey.Object == node)
            {
                image = imageKey.Image;
            }
        }

        return image;
    }

    /// <summary>그 애니메이션의 그림 키가 쓰는 그림 전부.</summary>
    public static IEnumerable<string> ImagesOf(TimelineAsset timeline) => (timeline.Flattened ?? timeline).ImageKeys.Select(s => s.Image);
}
