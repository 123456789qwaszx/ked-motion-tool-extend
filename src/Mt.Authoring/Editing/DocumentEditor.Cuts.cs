using Mt.Authoring.Graph;
using Mt.Authoring.Model;

namespace Mt.Authoring.Editing;

// 컷의 명령(PLAN-standalone.md §6) — 컷 = 오브젝트 계층 + 카메라 + 타임라인 하나. 각각 되돌리기 한 칸.
// 오브젝트를 놓고 옮기는 것은 세트의 명령 그대로다(컷의 이름이 곧 그 계층의 이름) — 여기에는 컷에만 있는 것(만들기·지우기·이름·길이·카메라·깊이)이 있다.
public sealed partial class DocumentEditor
{
    /// <summary>컷 길이의 상한(프레임) — 터무니없는 값이 타임라인을 메우지 않게(10분).</summary>
    public const int MaxCutFrames = CutAsset.Fps * 600;

    /// <summary>깊이(depthLevel)의 범위 — far(0)에서 close(20)의 두 배까지. 깊이 커브가 설계된 구간과 그 너머의 외삽.</summary>
    public const float MinDepth = 0f;
    public const float MaxDepth = 40f;

    /// <summary>
    /// 컷을 만든다 — 빈 오브젝트 계층 + 그 길이의 타임라인(같은 이름의 애니메이션 하나: 키는 그 프레임의 모습이다). 한 칸.
    /// 이름은 영문 소문자·숫자·_ 이고 세트·컷·애니메이션과 겹치면 안 된다.
    /// </summary>
    public CutAsset AddCut(string name, int frames = CutAsset.DefaultFrames)
    {
        string trimmed = RequireNewCutName(name);
        RequireCutFrames(frames);
        var cut = new CutAsset { Name = trimmed, Frames = frames };

        Batch(DocumentChangeKind.Document, () =>
        {
            Mutate(DocumentChangeKind.Document, () => Document.Cuts.Add(cut));
            AddTimeline(trimmed, trimmed, cut.Seconds);
        });

        return cut;
    }

    /// <summary>컷을 지운다 — 그 타임라인과 스킨(그림 표)도 함께. 그 컷이 품었던 세트는 남는다.</summary>
    public void RemoveCut(string name)
    {
        CutAsset cut = RequireCut(name);

        Mutate(DocumentChangeKind.Document, () =>
        {
            Document.Timelines.RemoveAll(m => m.Set == name);
            Document.Skins.RemoveAll(s => s.Set == name);
            Document.Cuts.Remove(cut);
        });
    }

    /// <summary>컷의 이름을 바꾼다 — 타임라인과 스킨이 따라온다.</summary>
    public void RenameCut(string name, string newName)
    {
        CutAsset cut = RequireCut(name);

        if (string.Equals(name, newName?.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        string trimmed = RequireNewCutName(newName);

        Batch(DocumentChangeKind.Document, () =>
        {
            Mutate(DocumentChangeKind.Document, () =>
            {
                cut.Name = trimmed;

                foreach (TimelineAsset timeline in Document.Timelines.Where(m => m.Set == name))
                {
                    timeline.Set = trimmed;
                }

                foreach (SkinAsset skin in Document.Skins.Where(s => s.Set == name))
                {
                    skin.Set = trimmed;
                }
            });

            if (Document.Find(name) is not null)
            {
                RenameTimeline(name, trimmed);
            }

            if (Document.FindSkin(name) is { } own && own.Set == trimmed && Document.FindSkin(trimmed) is null)
            {
                RenameSkin(name, trimmed);
            }
        });
    }

    /// <summary>컷의 길이(프레임)를 바꾼다 — 타임라인의 길이가 따라온다. 키는 옮기지 않는다(길이 밖의 키는 검증이 알린다).</summary>
    public void SetCutFrames(string name, int frames)
    {
        CutAsset cut = RequireCut(name);
        RequireCutFrames(frames);

        if (cut.Frames == frames)
        {
            return;
        }

        Batch(DocumentChangeKind.Document, () =>
        {
            Mutate(DocumentChangeKind.Document, () => cut.Frames = frames);

            if (Document.Find(name) is not null)
            {
                SetDuration(name, cut.Seconds);
            }
        });
    }

    /// <summary>컷의 카메라(가만히 선 밀기·당기기)를 바꾼다.</summary>
    public void SetCutCamera(string name, CameraSettings camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        CutAsset cut = RequireCut(name);

        if (!float.IsFinite(camera.Zoom) || !float.IsFinite(camera.PanX) || !float.IsFinite(camera.PanY))
        {
            throw new InvalidOperationException("카메라의 값은 수여야 한다.");
        }

        if (cut.Camera.Zoom == camera.Zoom && cut.Camera.PanX == camera.PanX && cut.Camera.PanY == camera.PanY)
        {
            return;
        }

        Mutate(DocumentChangeKind.Stage, () => cut.Camera = new CameraSettings { Zoom = camera.Zoom, PanX = camera.PanX, PanY = camera.PanY });
    }

    /// <summary>
    /// 컷의 타임라인에 **카메라의 키**를 놓는다 — 그 시각에 밀기(x·y)와 당기기 셋 다. 한 칸. 같은 시각에 키가 있으면 그 키를 대신한다.
    /// 카메라는 타임라인의 트랙이다(<see cref="Preview.CutStage.CameraPan"/> · <see cref="Preview.CutStage.CameraZoom"/>의 자리 채널).
    /// </summary>
    public void SetCutCameraKey(string name, float time, CameraSettings camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        CutAsset cut = RequireCut(name);
        TimelineAsset timeline = Document.Find(name) ?? throw new InvalidOperationException($"컷 '{name}'의 타임라인이 없다.");

        if (!float.IsFinite(time) || time < 0f || time > cut.Seconds + KeyTimes.Tolerance)
        {
            throw new InvalidOperationException($"키의 시각은 컷의 길이(0~{cut.Seconds:0.###}초) 안이다 ('{time}')");
        }

        if (!float.IsFinite(camera.Zoom) || !float.IsFinite(camera.PanX) || !float.IsFinite(camera.PanY))
        {
            throw new InvalidOperationException("카메라의 값은 수여야 한다.");
        }

        Batch(DocumentChangeKind.Keys, () =>
        {
            foreach (string node in new[] { Preview.CutStage.CameraPan, Preview.CutStage.CameraZoom })
            {
                if (timeline.FindChannel(node, ChannelKind.Position) is null)
                {
                    AddChannel(name, node, ChannelKind.Position);
                }
            }

            AddKey(name, Preview.CutStage.CameraPan, ChannelKind.Position, Axis.X, new KeyAsset { T = time, V = camera.PanX });
            AddKey(name, Preview.CutStage.CameraPan, ChannelKind.Position, Axis.Y, new KeyAsset { T = time, V = camera.PanY });
            AddKey(name, Preview.CutStage.CameraZoom, ChannelKind.Position, Axis.X, new KeyAsset { T = time, V = camera.Zoom });
        });
    }

    /// <summary>컷의 카메라에 키가 있는가 — 있으면 카메라 칸의 값은 키로 들어간다(없으면 가만히 선 값을 고친다).</summary>
    public bool CutCameraIsKeyed(string name)
        => Document.Find(name) is { } timeline
           && timeline.Channels.Any(c => Preview.CutStage.IsCameraNode(c.Object) && new[] { c.X, c.Y, c.Z }.Any(curve => curve is { Keys.Count: > 0 }));

    /// <summary>
    /// 컷의 맨 위 오브젝트의 깊이(depthLevel)를 준다 — null이면 mid. 밑의 오브젝트는 부모를 따르므로 깊이를 갖지 않는다.
    /// </summary>
    public void SetCutNodeDepth(string cutName, string id, float? depth)
    {
        CutAsset cut = RequireCut(cutName);
        ObjectAsset node = cut.AsSet.Find(id) ?? throw new KeyNotFoundException($"오브젝트 '{id}'가 컷 '{cutName}'에 없다.");

        if (node.Parent.Length > 0)
        {
            throw new InvalidOperationException($"'{id}'는 '{node.Parent}' 밑의 오브젝트다 — 깊이는 맨 위 오브젝트만 가진다(밑의 것은 부모를 따른다).");
        }

        if (depth is { } value && (!float.IsFinite(value) || value < MinDepth || value > MaxDepth))
        {
            throw new InvalidOperationException($"깊이는 {MinDepth:0}~{MaxDepth:0}의 수다 ('{depth}')");
        }

        if (node.Depth != depth)
        {
            Mutate(DocumentChangeKind.Document, () => node.Depth = depth);
        }
    }

    /// <summary>
    /// 컷의 타임라인에 **깊이의 키**를 놓는다 — 그 시각에 그 맨 위 오브젝트의 깊이. 한 칸. 깊이는 타임라인의 트랙이다(<see cref="Preview.CutStage.DepthNodeOf"/>의 자리 채널 x).
    /// </summary>
    public void SetCutDepthKey(string cutName, string id, float time, float depth)
    {
        CutAsset cut = RequireCut(cutName);
        ObjectAsset node = cut.AsSet.Find(id) ?? throw new KeyNotFoundException($"오브젝트 '{id}'가 컷 '{cutName}'에 없다.");
        TimelineAsset timeline = Document.Find(cutName) ?? throw new InvalidOperationException($"컷 '{cutName}'의 타임라인이 없다.");

        if (node.Parent.Length > 0)
        {
            throw new InvalidOperationException($"'{id}'는 '{node.Parent}' 밑의 오브젝트다 — 깊이는 맨 위 오브젝트만 가진다.");
        }

        if (!float.IsFinite(depth) || depth < MinDepth || depth > MaxDepth)
        {
            throw new InvalidOperationException($"깊이는 {MinDepth:0}~{MaxDepth:0}의 수다 ('{depth}')");
        }

        if (!float.IsFinite(time) || time < 0f || time > cut.Seconds + KeyTimes.Tolerance)
        {
            throw new InvalidOperationException($"키의 시각은 컷의 길이(0~{cut.Seconds:0.###}초) 안이다 ('{time}')");
        }

        string point = Preview.CutStage.DepthNodeOf(id);

        Batch(DocumentChangeKind.Keys, () =>
        {
            if (timeline.FindChannel(point, ChannelKind.Position) is null)
            {
                AddChannel(cutName, point, ChannelKind.Position);
            }

            AddKey(cutName, point, ChannelKind.Position, Axis.X, new KeyAsset { T = time, V = depth });
        });
    }

    /// <summary>그 오브젝트의 깊이에 키가 있는가.</summary>
    public bool CutDepthIsKeyed(string cutName, string id)
        => Document.Find(cutName)?.FindChannel(Preview.CutStage.DepthNodeOf(id), ChannelKind.Position) is { X.Keys.Count: > 0 };

    private CutAsset RequireCut(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Document.FindCut(name) ?? throw new KeyNotFoundException($"컷 '{name}'이 문서에 없다.");
    }

    private string RequireNewCutName(string? name)
    {
        string trimmed = (name ?? "").Trim();

        if (!NameRules.IsValid(trimmed) || !char.IsAsciiLetter(trimmed[0]))
        {
            throw new InvalidOperationException($"컷 이름은 영문 소문자로 시작하는 영문 소문자·숫자·_ 다 ('{name}')");
        }

        if (Document.FindSetOrCut(trimmed) is not null)
        {
            throw new InvalidOperationException($"'{trimmed}'라는 세트나 컷이 이미 있다.");
        }

        if (Document.Find(trimmed) is not null)
        {
            throw new InvalidOperationException($"'{trimmed}'라는 애니메이션이 이미 있다 — 컷의 타임라인이 그 이름을 쓴다.");
        }

        return trimmed;
    }

    private static void RequireCutFrames(int frames)
    {
        if (frames < 1 || frames > MaxCutFrames)
        {
            throw new InvalidOperationException($"컷의 길이는 1~{MaxCutFrames}프레임이다 ('{frames}')");
        }
    }
}
