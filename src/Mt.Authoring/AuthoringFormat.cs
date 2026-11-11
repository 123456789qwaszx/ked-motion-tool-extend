namespace Mt.Authoring;

/// <summary>
/// 저작 원본(<c>*.cut-authoring.json</c>)의 스키마 이름. 이 툴의 살림이다 — 세트와 컷, 탄젠트 모드, 재생 블록처럼 컷 파일에 구워져 나가기 전의 것을 담는다.
/// 산출물은 컷 파일(<c>cut/1</c>) 하나이고 그 이름은 코어(<c>CutPlayer.Format</c>)에 있다.
/// </summary>
public static class AuthoringFormat
{
    /// <summary>저작 원본 파일의 스키마.</summary>
    public const string AuthoringSchema = "cut-authoring/1";

    /// <summary>저작 원본 파일 확장자.</summary>
    public const string AuthoringExtension = ".cut-authoring.json";
}
