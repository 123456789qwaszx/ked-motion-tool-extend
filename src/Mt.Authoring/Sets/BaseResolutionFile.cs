using Ked.Presentation.Core;

namespace Mt.Authoring.Sets;

/// <summary>기준 해상도 — 무대 크기이자 코어 <c>RectSpace</c>의 크기. 1920×1080으로 고정이다.</summary>
public static class BaseResolutionFile
{
    public static readonly Vec2 Default = new(1920f, 1080f);
}
