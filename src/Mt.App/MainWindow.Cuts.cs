using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;

namespace Mt.App;

// 컷의 셸(PLAN-standalone.md §6 · C2c) — 왼쪽 맨 위. 컷 = 씬(오브젝트 계층) + 카메라 + 타임라인 하나.
// 컷을 열면 오브젝트를 바로 놓는다(하이어아키 우클릭 · 그림 가져오기 · 층 표) — 세트를 먼저 만들지 않는다. 세트는 그 아래의 "묶음"이다.
// 여는 길은 세트와 같다: 뷰포트와 하이어아키가 그 컷만 보이고, 오브젝트를 고치는 칸도 같은 칸이다.
public partial class MainWindow
{
    private readonly ListBox _cutList = new() { MaxHeight = 110, FontSize = 12 };
    private readonly TextBox _cutName = new() { FontSize = 11, PlaceholderText = "(컷을 연다)" };
    private readonly TextBox _cutFrames = new() { FontSize = 11, Width = 60 };
    private readonly TextBox _cutZoom = new() { FontSize = 11, Width = 48 };
    private readonly TextBox _cutPanX = new() { FontSize = 11, Width = 56 };
    private readonly TextBox _cutPanY = new() { FontSize = 11, Width = 56 };
    private readonly StackPanel _cutFields = new() { Spacing = 3 };
    private readonly Button _cutCameraKey = new() { Content = "키", FontSize = 11, Padding = new Avalonia.Thickness(6, 1), Margin = new Avalonia.Thickness(3, 0, 0, 0) };
    private readonly TextBox _objectDepth = new() { FontSize = 11, PlaceholderText = "(mid 14)" };
    private readonly Button _objectDepthKey = new() { Content = "키", FontSize = 11, Padding = new Avalonia.Thickness(6, 1), Margin = new Avalonia.Thickness(3, 0, 0, 0) };
    private Control? _objectDepthRow;
    private bool _switchingLists;

    /// <summary>테스트 손잡이 — 컷 목록의 선택, "새 컷", 컷의 칸들(이름 · 길이 · 카메라 당기기 · 밀기 X · 밀기 Y), 오브젝트의 깊이 칸.</summary>
    internal string? SelectedCut => _cutList.SelectedItem as string;

    internal string NewCutFromForm() => NewCut();

    internal void RemoveCutFromForm() => RemoveSelectedCut();

    /// <summary>테스트 손잡이 — "컷 내보내기…"(파일 고르기 뒤의 일).</summary>
    internal bool ExportCutToFile(string path) => SelectedCut is { } name && _session.ExportCut(name, path);

    internal (TextBox Name, TextBox Frames, TextBox Zoom, TextBox PanX, TextBox PanY) CutBoxes => (_cutName, _cutFrames, _cutZoom, _cutPanX, _cutPanY);

    internal TextBox ObjectDepthBox => _objectDepth;

    /// <summary>테스트 손잡이 — 카메라의 "키" 단추.</summary>
    internal void KeyCutDepthFromForm() => _objectDepthKey.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    internal void KeyCutCameraFromForm() => _cutCameraKey.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    internal bool ObjectDepthShown => _objectDepthRow?.IsVisible == true;

    private CutAsset? OpenCut => _session.Editor.Document.FindCut(SelectedCut);

    /// <summary>연 것의 이름표 — "컷 x" 또는 "세트 x".</summary>
    private string KindLabel(string kind) => _session.Editor.Document.FindCut(kind) is null ? $"세트 {kind}" : $"컷 {kind}";

    private void BuildCutLeft()
    {
        LeftPanel.Children.Insert(0, Header("컷"));
        ToolTip.SetTip(_cutList, "컷 — 씬(계층 구조의 오브젝트들) + 카메라 + 정해진 길이의 타임라인. 캐릭터·배경·소품이 전부 오브젝트이고, 그 기간의 움직임을 키프레임으로 잡는다. 고르면 뷰포트와 하이어아키가 그 컷을 보인다");
        LeftPanel.Children.Insert(1, _cutList);

        Button add = SmallButton("새 컷", (_, _) => Guard("새 컷", () => NewCut()));
        Button remove = SmallButton("컷 삭제", (_, _) => Guard("컷 삭제", RemoveSelectedCut));
        Button close = SmallButton("닫기", (_, _) => SelectSet(null));
        ToolTip.SetTip(add, "빈 컷(240프레임 = 10초)을 만들어 연다 — 이름 cut, cut_2…. 하이어아키에서 우클릭해 오브젝트를 놓거나, 아래 세트 줄의 \"그림 가져오기…\" · \"층 표 가져오기…\"로 그림을 들인다");
        ToolTip.SetTip(remove, "연 컷을 지운다 — 그 타임라인과 그림 표도 함께(되돌리기 한 번). 그 컷이 품은 세트는 남는다");
        ToolTip.SetTip(close, "컷을 닫는다");
        Button export = SmallButton("컷 내보내기…", async (_, _) =>
        {
            try
            {
                if (SelectedCut is { } name
                    && await PickSaveAsync("컷 내보내기", name + ".cut.json", new Avalonia.Platform.Storage.FilePickerFileType("컷 파일") { Patterns = new[] { "*.cut.json", "*.json" } }) is { } path)
                {
                    _session.ExportCut(name, path);
                }
            }
            catch (Exception exception)
            {
                _session.SetStatus($"컷 내보내기 실패: {exception.Message}");
            }
        });
        ToolTip.SetTip(export, "연 컷을 컷 파일(cut/1)로 내보낸다 — 게임이 읽는 것. 묶음(세트)은 오브젝트로 펼치고 재생 블록은 키로 구운 한 덩어리이고, 시각은 프레임으로 적힌다. 그림은 복사하지 않고 그 파일 기준 경로로 적는다");
        LeftPanel.Children.Insert(2, new WrapPanel { Orientation = Orientation.Horizontal, Children = { add, remove, close, export } });

        _cutFields.Children.Add(Labeled("컷 이름", _cutName));
        _cutFields.Children.Add(Labeled("길이(프레임)", _cutFrames));
        _cutFields.Children.Add(Labeled("카메라", new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _cutZoom, _cutPanX, _cutPanY, _cutCameraKey },
        }));
        ToolTip.SetTip(_cutName, "연 컷의 이름 — 고치고 Enter. 타임라인과 그림 표가 따라온다. 영문 소문자로 시작하는 영문 소문자·숫자·_");
        ToolTip.SetTip(_cutFrames, $"컷의 길이(프레임, {CutAsset.Fps}fps — 240 = 10초). 타임라인의 길이가 따라온다. 키는 옮기지 않는다");
        ToolTip.SetTip(_cutZoom, "카메라 당기기(zoom) — 1당 배율 0.05. 가까운 오브젝트일수록 더 커진다");
        ToolTip.SetTip(_cutPanX, "카메라 밀기 X(pan, px) — 먼 오브젝트는 덜, 가까운 오브젝트는 더 따라온다");
        ToolTip.SetTip(_cutPanY, "카메라 밀기 Y(pan, px)");
        LeftPanel.Children.Insert(3, _cutFields);

        _cutList.SelectionChanged += (_, _) => OnListPicked(_cutList, _setList);
        OnCommit(_cutName, text => WithOpenCut(cut =>
        {
            if (text.Trim() != cut.Name)
            {
                _session.Editor.RenameCut(cut.Name, text);
                RefreshSets();
                SelectSet(text.Trim());
            }
        }));
        OnCommit(_cutFrames, text => WithOpenCut(cut =>
        {
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int frames))
            {
                _session.Editor.SetCutFrames(cut.Name, frames);
            }
        }));

        // 카메라 칸 — 카메라에 키가 없으면 가만히 선 값을, 있으면 지금 프레임의 키를 고친다(마야: 키 있는 값을 고치면 그 프레임의 키가 된다).
        foreach (TextBox box in new[] { _cutZoom, _cutPanX, _cutPanY })
        {
            OnCommit(box, _ => WithOpenCut(cut =>
            {
                CameraSettings camera = CameraFromBoxes(cut);

                if (_session.Editor.CutCameraIsKeyed(cut.Name))
                {
                    _session.Editor.SetCutCameraKey(cut.Name, CutTimeNow(cut), camera);
                }
                else
                {
                    _session.Editor.SetCutCamera(cut.Name, camera);
                }
            }));
        }

        _cutCameraKey.Click += (_, _) => WithOpenCut(cut => _session.Editor.SetCutCameraKey(cut.Name, CutTimeNow(cut), CameraFromBoxes(cut)));
        ToolTip.SetTip(_cutCameraKey, "카메라의 키 — 지금 프레임에 카메라 칸의 값(당기기 · 밀기 X · 밀기 Y)으로 키를 놓는다. 카메라는 타임라인의 트랙이다(그래프에 Camera_Pan · Camera_Zoom으로 보인다). 키가 한 번 놓이면 그 뒤로 칸을 고치는 것이 그 프레임의 키가 된다");
        _viewport.TimeChanged += (_, _) => RefreshCutFields(); // 카메라·깊이 칸은 지금 프레임의 값을 보인다
        _objectDepthKey.Click += (_, _) => WithOpenCut(cut =>
        {
            if (EditedObject is { Parent.Length: 0 } node)
            {
                float depth = TryFloat(_objectDepth.Text ?? "", out float typed) ? typed : _viewport.CutDepthNow(node.Id) ?? CutStage.DefaultDepth;
                _session.Editor.SetCutDepthKey(cut.Name, node.Id, CutTimeNow(cut), depth);
            }
        });
        ToolTip.SetTip(_objectDepthKey, "깊이의 키 — 지금 프레임에 이 오브젝트의 깊이로 키를 놓는다(오브젝트가 멀어지고 가까워진다). 깊이는 타임라인의 트랙이다(그래프에 이름__depth로 보인다). 키가 한 번 놓이면 그 뒤로 칸을 고치는 것이 그 프레임의 키가 된다");

        // 오브젝트의 깊이 — 컷의 맨 위 오브젝트에서만 선다("오브젝트" 탭의 칸 안, 부모 칸 아래 — 줄은 그 칸을 지을 때 선다).
        ToolTip.SetTip(_objectDepth, $"깊이(depthLevel) — far 0 · back 10 · mid 14 · front 16 · close 20, {DocumentEditor.MinDepth:0}~{DocumentEditor.MaxDepth:0}. 멀수록 작게 보이고 카메라에 덜 따라 움직이며 뒤에 그려진다. 비우면 mid. 맨 위 오브젝트만 가진다(밑의 것은 부모를 따른다)");
        OnCommit(_objectDepth, text => WithOpenCut(cut =>
        {
            if (EditedObject is { } node)
            {
                string trimmed = text.Trim();

                if (trimmed.Length == 0)
                {
                    _session.Editor.SetCutNodeDepth(cut.Name, node.Id, null);
                }
                else if (TryFloat(trimmed, out float depth))
                {
                    // 깊이에 키가 있으면 지금 프레임의 키로, 없으면 가만히 선 값으로(카메라 칸과 같다).
                    if (_session.Editor.CutDepthIsKeyed(cut.Name, node.Id))
                    {
                        _session.Editor.SetCutDepthKey(cut.Name, node.Id, CutTimeNow(cut), depth);
                    }
                    else
                    {
                        _session.Editor.SetCutNodeDepth(cut.Name, node.Id, depth);
                    }
                }
            }
        }));
    }

    /// <summary>컷 목록과 세트 목록은 하나만 고른다 — 한쪽을 고르면 다른 쪽이 풀린다.</summary>
    private void OnListPicked(ListBox picked, ListBox other)
    {
        if (_refreshing || _switchingLists)
        {
            return;
        }

        if (picked.SelectedItem is not null && other.SelectedItem is not null)
        {
            _switchingLists = true;

            try
            {
                other.SelectedItem = null;
            }
            finally
            {
                _switchingLists = false;
            }
        }

        OnSetSelected();

        // 컷을 열면 그 타임라인이 바로 붙는다 — 따로 고르지 않는다(컷에는 타임라인이 하나다).
        if (SelectedCut is { } cut && _session.Editor.Document.Find(cut) is not null)
        {
            RefreshTimelineList();
            _timelineList.SelectedItem = cut;
        }
    }

    private void WithOpenCut(Action<CutAsset> action)
    {
        if (_refreshing || OpenCut is not { } cut)
        {
            return;
        }

        Guard("컷", () => action(cut));
        RefreshCutFields();
    }

    private CameraSettings CameraFromBoxes(CutAsset cut)
    {
        CameraSettings now = _viewport.CutCameraNow() ?? cut.Camera;
        float Value(TextBox from, float fallback) => TryFloat(from.Text ?? "", out float parsed) ? parsed : fallback;
        return new CameraSettings { Zoom = Value(_cutZoom, now.Zoom), PanX = Value(_cutPanX, now.PanX), PanY = Value(_cutPanY, now.PanY) };
    }

    /// <summary>지금 시각(초) — 컷의 길이 안으로.</summary>
    private float CutTimeNow(CutAsset cut) => (float)Math.Clamp(_graph.CurrentTime, 0, cut.Seconds);

    private string NewCut()
    {
        string name = "cut";

        for (int i = 2; _session.Editor.Document.FindSetOrCut(name) is not null || _session.Editor.Document.Find(name) is not null; i++)
        {
            name = $"cut_{i.ToString(CultureInfo.InvariantCulture)}";
        }

        _session.Editor.AddCut(name);
        RefreshSets();
        SelectSet(name);
        return name;
    }

    private void RemoveSelectedCut()
    {
        if (SelectedCut is { } name)
        {
            _session.Editor.RemoveCut(name);
        }
    }

    /// <summary>컷 목록과 컷의 칸들을 문서에 맞춘다 — <see cref="RefreshSets"/>가 부른다.</summary>
    private void RefreshCutList()
    {
        string? selected = SelectedCut;
        List<string> names = _session.Editor.Document.Cuts.Select(c => c.Name).ToList();
        _cutList.ItemsSource = names;
        _cutList.SelectedIndex = selected is null ? -1 : names.IndexOf(selected); // 지워졌으면(되돌리기 등) 풀린다
    }

    private void RefreshCutFields()
    {
        _refreshDepth++;

        try
        {
            CutAsset? cut = OpenCut;
            _cutFields.IsEnabled = cut is not null;

            void Write(TextBox box, string text)
            {
                if (!box.IsFocused)
                {
                    box.Text = text;
                }
            }

            Write(_cutName, cut?.Name ?? "");
            Write(_cutFrames, cut?.Frames.ToString(CultureInfo.InvariantCulture) ?? "");
            CameraSettings? camera = cut is null ? null : _viewport.CutCameraNow() ?? cut.Camera; // 지금 프레임의 값
            Write(_cutZoom, camera is null ? "" : F(camera.Zoom));
            Write(_cutPanX, camera is null ? "" : F(camera.PanX));
            Write(_cutPanY, camera is null ? "" : F(camera.PanY));

            // 깊이 칸 — 컷의 맨 위 오브젝트일 때만.
            ObjectAsset? node = EditedObject;
            bool top = cut is not null && node is { Parent.Length: 0 };

            if (_objectDepthRow is not null)
            {
                _objectDepthRow.IsVisible = top;
            }

            // 키가 있으면 지금 프레임의 값, 없으면 적힌 값(안 적혔으면 빈 칸 = mid).
            bool depthKeyed = top && _session.Editor.CutDepthIsKeyed(cut!.Name, node!.Id);
            Write(_objectDepth, !top ? "" : depthKeyed ? F(_viewport.CutDepthNow(node!.Id) ?? CutStage.DefaultDepth) : node!.Depth is { } depth ? F(depth) : "");
        }
        finally
        {
            _refreshDepth--;
        }
    }
}
