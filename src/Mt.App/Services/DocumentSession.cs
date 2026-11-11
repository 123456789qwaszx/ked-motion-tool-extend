using Mt.Authoring;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Sets;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;
using Ked.Presentation.Core;

namespace Mt.App.Services;

/// <summary>
/// 열린 문서 하나의 살림 — 경로·편집기·세운 리그·저장 여부. 화면은 이것을 통해서만 파일에 닿는다.
/// 파일 선택 창은 화면의 일이고, 여기는 경로를 받아 읽고 쓴다(헤드리스 테스트가 그대로 쓴다).
/// </summary>
public sealed class DocumentSession
{
    private long _savedRevision;

    public DocumentSession()
    {
        Editor = new DocumentEditor(new AuthoringDocument());
        Editor.Changed += OnEditorChanged;
        _assetFingerprint = AssetFingerprintOf(Editor.Document);
    }

    // ── 문서 ↔ 실은 자산 ──────────────────────────────────────────────
    //
    // 문서가 정의한 리그(세트 · 컷)와 스킨의 그림은 되돌리기·다시하기로도 바뀐다. 그때 세운 리그와 뷰포트의 그림 비트맵이 문서와 어긋나지 않도록,
    // 지문을 두고 달라진 때만 AssetsChanged를 올린다. 키 편집(Keys·Metadata)은 보지도 않는다.

    private string _assetFingerprint = "";

    /// <summary>테스트 손잡이 — 문서를 따라 자산을 다시 실은 횟수.</summary>
    internal int AssetSyncCount { get; private set; }

    private static string AssetFingerprintOf(AuthoringDocument document)
        => "sets " + SetsKeyOf(document)
           + "\n" + string.Join("\n", document.Skins.Select(s => $"skin {s.Name}:{s.Set}:" + string.Join(",", s.Parts.Keys.Select(node => node + "=" + string.Join("|", s.ImagesOf(node))))));

    private void OnEditorChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.Kind is DocumentChangeKind.Keys or DocumentChangeKind.Metadata)
        {
            return;
        }

        string fingerprint = AssetFingerprintOf(Editor.Document);

        if (fingerprint == _assetFingerprint)
        {
            return;
        }

        _assetFingerprint = fingerprint;
        AssetSyncCount++;
        AssetsChanged?.Invoke(this, EventArgs.Empty); // 리그와 그림은 뷰포트가 문서대로 다시 싣는다
    }

    public DocumentEditor Editor { get; private set; }

    public string? DocumentPath { get; private set; }

    /// <summary>
    /// 문서가 정의한 것(세트 · 컷)만으로 세운 리그 한 벌 — 뷰포트·검증·하이어아키가 이것을 읽는다. 문서의 리그가 바뀌면 다시 세운다.
    /// 세울 수 없는 리그가 있으면 빈 한 벌로 물러나고 상태 줄에 남긴다.
    /// </summary>
    public BuiltSets Built
    {
        get
        {
            string key = SetsKeyOf(Editor.Document);

            if (_built is null || key != _setKey)
            {
                _setKey = key;

                try
                {
                    _built = SetBuilder.Compose(Editor.Document, StageSize);
                }
                catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or System.Text.Json.JsonException or ArgumentException)
                {
                    _built = BuiltSets.Empty(StageSize);
                    Status = $"문서의 세트·컷을 세우지 못했다: {exception.Message}";
                }
            }

            return _built;
        }
    }

    private BuiltSets? _built;
    private string _setKey = "";

    private static string SetsKeyOf(AuthoringDocument document)
        => string.Join("|", document.AllSetsAndCuts.Select(r => r.Name + ":" + string.Join(",", r.Objects.Select(n => $"{n.Id}<{n.Parent}@{n.X},{n.Y}#{n.Width}x{n.Height}r{n.Rotation}s{n.ScaleX},{n.ScaleY}c{n.Color}S{n.Set}{(n.Image ? "i" : "")}{(n.Alpha ? "a" : "")}"))));

    /// <summary>
    /// 컷을 컷 파일(<c>cut/1</c>)로 내보낸다 — 다 펼쳐 구운 한 덩어리. 그림 경로는 그 파일 기준으로 적힌다(그림은 복사하지 않는다).
    /// 막는 문제가 있는 타임라인이면 안 나간다.
    /// </summary>
    public bool ExportCut(string cutName, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (Editor.Document.FindCut(cutName) is not { } cut)
        {
            SetStatus($"컷 '{cutName}'이 없다.");
            return false;
        }

        List<ValidationProblem> blocking = DocumentValidator.BlockingCutExport(Editor.Document, cutName, ObjectIds, Built.AlphaNodeIds);

        if (blocking.Count > 0)
        {
            SetStatus($"컷 내보내기 막힘 — 문제 {blocking.Count}건을 먼저 고친다: {blocking[0]}");
            return false;
        }

        string folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        CutFileDto file = CutFileJson.Build(Editor.Document, cut, StageSize,
            image => Path.GetRelativePath(folder, ResolveFromDocument(image)).Replace('\\', '/'));
        File.WriteAllBytes(path, new System.Text.UTF8Encoding(false).GetBytes(CutFileJson.Write(file)));
        SetStatus($"내보냈다: {path} — 컷 {cut.Name}({cut.Frames}프레임 · 오브젝트 {file.objects.Count}개 · 트랙 {file.tracks.Count + file.images.Count}개, {CutPlayer.Format})");
        return true;
    }

    /// <summary>깊이 커브(내장) — 컷의 깊이가 이것으로 크기를 정한다.</summary>
    public DepthTuningFile Depth => BuiltinTuning.Depth;

    public string Status { get; private set; } = "";

    /// <summary>문서의 스킨들이 쓰는 그림 전부 — 문서 기준 경로 → 파일 경로(파일이 없으면 null: 자리표시로 그린다).</summary>
    public IReadOnlyList<(string Image, string? Path)> SkinImages()
        => Editor.Document.Skins.SelectMany(s => s.AllImages()).Concat(Editor.Document.Timelines.SelectMany(Mt.Authoring.Preview.ImageKeys.ImagesOf)).Distinct(StringComparer.Ordinal).Select(image => // 그림 키의 그림까지
        {
            string resolved = ResolveFromDocument(image);
            return (image, File.Exists(resolved) ? resolved : (string?)null);
        }).ToList();

    /// <summary>
    /// 층 표(<c>layers.tsv</c>)를 세트로 들인다 — 표 옆의 그림들이 오브젝트 여럿 + 스킨으로, 한 칸에(<c>PLAN-set.md</c> S6). 그림 경로는 문서 기준으로 적힌다.
    /// </summary>
    public IReadOnlyList<string> ImportLayers(string kind, string sheetPath, string skin)
    {
        ArgumentNullException.ThrowIfNull(sheetPath);
        LayerSheet sheet = LayerSheet.Load(sheetPath);
        string folder = Path.GetDirectoryName(Path.GetFullPath(sheetPath)) ?? "";
        // 표의 파일 하나를 문서 기준 경로로 바꿔 그 폴더를 얻는다(저장 전의 문서면 절대 경로로 남는다).
        string probe = RelativeToDocument(Path.Combine(folder, "x")).Replace('\\', '/');
        string relative = probe.Length > 1 ? probe[..^2] : "";
        return Editor.ImportLayers(kind, sheet, relative, skin, StageSize);
    }

    /// <summary>다른 저작 문서의 세트를 전부 들인다(애니메이션·스킨까지) — 그림 경로는 이 문서 기준으로 바뀐다. 들인 세트의 이름들.</summary>
    public IReadOnlyList<string> ImportSets(string documentPath)
    {
        ArgumentNullException.ThrowIfNull(documentPath);
        AuthoringDocument source = AuthoringDocumentJson.Read(File.ReadAllText(documentPath));
        string folder = Path.GetDirectoryName(Path.GetFullPath(documentPath)) ?? "";
        IReadOnlyList<string> kinds = Editor.ImportSets(source, null, image => Path.IsPathRooted(image) ? image : RelativeToDocument(Path.Combine(folder, image)).Replace('\\', '/'));
        SetStatus($"세트 {kinds.Count}개를 들였다 — {string.Join(", ", kinds)}");
        return kinds;
    }

    /// <summary>스킨의 조각 그림을 파일로 정한다 — 경로는 문서 기준 상대로 적힌다. null이면 그 조각을 비운다.</summary>
    public void SetSkinPart(string skin, string node, string? path)
        => Editor.SetSkinPart(skin, node, string.IsNullOrEmpty(path) ? null : RelativeToDocument(path));

    /// <summary>기준 해상도(무대 크기) — 1920×1080.</summary>
    public Vec2 StageSize => BaseResolutionFile.Default;

    public bool IsDirty => Editor.Revision != _savedRevision;

    /// <summary>문서·편집기가 통째로 갈렸다(새로/열기). 화면은 편집기를 다시 묶는다.</summary>
    public event EventHandler? DocumentReplaced;

    public event EventHandler? StatusChanged;

    /// <summary>세운 리그나 그림이 바뀌었다. 뷰포트가 다시 싣는다.</summary>
    public event EventHandler? AssetsChanged;

    /// <summary>검증에 쓰는 setName → 노드 표.</summary>
    public IReadOnlyDictionary<string, ICollection<string>> ObjectIds => Built.NodeIds;

    public IReadOnlyList<ValidationProblem> Problems => DocumentValidator.Validate(Editor.Document, ObjectIds, Built.AlphaNodeIds);

    public void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void New()
    {
        Replace(new AuthoringDocument(), null);
        SetStatus("새 문서.");
    }

    public void Open(string path)
    {
        AuthoringDocument document = AuthoringDocumentJson.Read(File.ReadAllText(path));
        Replace(document, path);
        SetStatus($"열었다: {path}");
    }

    /// <summary>자동 저장 — 문서 옆 <c>*.autosave.cut-authoring.json</c>. 저장 여부(IsDirty)는 건드리지 않는다. 경로가 없으면 안 한다.</summary>
    public string? AutoSave()
    {
        if (DocumentPath is null || !IsDirty)
        {
            return null;
        }

        string path = AutoSavePathFor(DocumentPath);
        File.WriteAllText(path, AuthoringDocumentJson.Write(Editor.Document));
        SetStatus($"자동 저장: {Path.GetFileName(path)}");
        return path;
    }

    public static string AutoSavePathFor(string documentPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(documentPath)) ?? ".";
        string name = Path.GetFileName(documentPath);
        string stem = name.EndsWith(AuthoringFormat.AuthoringExtension, StringComparison.OrdinalIgnoreCase)
            ? name[..^AuthoringFormat.AuthoringExtension.Length]
            : Path.GetFileNameWithoutExtension(name);
        return Path.Combine(directory, stem + ".autosave" + AuthoringFormat.AuthoringExtension);
    }

    public void Save(string? path = null)
    {
        path ??= DocumentPath ?? throw new InvalidOperationException("저장할 경로가 없다 — 다른 이름으로 저장을 쓴다.");
        File.WriteAllText(path, AuthoringDocumentJson.Write(Editor.Document));
        DocumentPath = path;
        _savedRevision = Editor.Revision;
        SetStatus($"저장했다: {path}");
    }

    private void Replace(AuthoringDocument document, string? path)
    {
        Editor.Changed -= OnEditorChanged;
        Editor = new DocumentEditor(document);
        Editor.Changed += OnEditorChanged;
        DocumentPath = path;
        _savedRevision = Editor.Revision;
        _built = null;
        _assetFingerprint = AssetFingerprintOf(Editor.Document);
        DocumentReplaced?.Invoke(this, EventArgs.Empty);
    }

    public string RelativeToDocument(string path)
    {
        string full = Path.GetFullPath(path);

        if (DocumentPath is null)
        {
            return full.Replace('\\', '/');
        }

        string documentDirectory = Path.GetDirectoryName(Path.GetFullPath(DocumentPath)) ?? ".";
        return Path.GetRelativePath(documentDirectory, full).Replace('\\', '/');
    }

    private string ResolveFromDocument(string path)
    {
        if (Path.IsPathRooted(path) || DocumentPath is null)
        {
            return Path.GetFullPath(path);
        }

        string documentDirectory = Path.GetDirectoryName(Path.GetFullPath(DocumentPath)) ?? ".";
        return Path.GetFullPath(Path.Combine(documentDirectory, path));
    }
}
