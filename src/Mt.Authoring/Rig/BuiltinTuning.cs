using System.Reflection;

namespace Mt.Authoring.Rig;

/// <summary>
/// 내장 기본값 — 런타임 <c>ExportedTuning/</c>에서 가져온 깊이 커브(<c>presets/depth.json</c>). 컷의 깊이가 이 커브로 크기를 정한다.
/// 사본은 손으로 고치지 않는다(<see cref="Source"/>에 원본을 적고, 갱신은 통째 교체).
/// </summary>
public static class BuiltinTuning
{
    /// <summary>원본 — 무대 런타임 커밋과 익스포트 날짜.</summary>
    public const string Source = "ked-stage-runtime dc690c1 · ExportedTuning 2026-08-21";

    private const string Prefix = "Defaults/";

    private static readonly Lazy<DepthTuningFile> DepthLazy = new(() => DepthTuningFile.Parse(Text("ExportedTuning/presets/depth.json")));

    public static DepthTuningFile Depth => DepthLazy.Value;

    private static string Text(string relative)
    {
        Assembly assembly = typeof(BuiltinTuning).Assembly;
        string wanted = Prefix + relative;

        // LogicalName의 구분자는 빌드 OS를 따른다 — 양쪽 다 받아 준다.
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (string.Equals(name.Replace('\\', '/'), wanted, StringComparison.Ordinal))
            {
                using Stream stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"내장 리소스를 열 수 없다: {name}");
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        throw new InvalidOperationException($"내장 리소스가 없다: {wanted}");
    }
}
