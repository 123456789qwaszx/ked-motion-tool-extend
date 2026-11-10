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
    private readonly TextBox _rigNodeDepth = new() { FontSize = 11, PlaceholderText = "(mid 14)" };
    private Control? _rigNodeDepthRow;
    private bool _switchingLists;

    /// <summary>테스트 손잡이 — 컷 목록의 선택, "새 컷", 컷의 칸들(이름 · 길이 · 카메라 당기기 · 밀기 X · 밀기 Y), 오브젝트의 깊이 칸.</summary>
    internal string? SelectedCut => _cutList.SelectedItem as string;

    internal string NewCutFromForm() => NewCut();

    internal void RemoveCutFromForm() => RemoveSelectedCut();

    internal (TextBox Name, TextBox Frames, TextBox Zoom, TextBox PanX, TextBox PanY) CutBoxes => (_cutName, _cutFrames, _cutZoom, _cutPanX, _cutPanY);

    internal TextBox RigNodeDepthBox => _rigNodeDepth;

    internal bool RigNodeDepthShown => _rigNodeDepthRow?.IsVisible == true;

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
        LeftPanel.Children.Insert(2, new WrapPanel { Orientation = Orientation.Horizontal, Children = { add, remove, close } });

        _cutFields.Children.Add(Labeled("컷 이름", _cutName));
        _cutFields.Children.Add(Labeled("길이(프레임)", _cutFrames));
        _cutFields.Children.Add(Labeled("카메라", new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _cutZoom, _cutPanX, _cutPanY },
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

        foreach (TextBox box in new[] { _cutZoom, _cutPanX, _cutPanY })
        {
            OnCommit(box, _ => WithOpenCut(cut =>
            {
                float Value(TextBox from, float fallback) => TryFloat(from.Text ?? "", out float parsed) ? parsed : fallback;
                _session.Editor.SetCutCamera(cut.Name, new CameraSettings
                {
                    Zoom = Value(_cutZoom, cut.Camera.Zoom),
                    PanX = Value(_cutPanX, cut.Camera.PanX),
                    PanY = Value(_cutPanY, cut.Camera.PanY),
                });
            }));
        }

        // 오브젝트의 깊이 — 컷의 맨 위 오브젝트에서만 선다("세트의 오브젝트" 칸 안, 부모 칸 아래 — 줄은 그 칸을 지을 때 선다).
        ToolTip.SetTip(_rigNodeDepth, $"깊이(depthLevel) — far 0 · back 10 · mid 14 · front 16 · close 20, {MotionEditor.MinDepth:0}~{MotionEditor.MaxDepth:0}. 멀수록 작게 보이고 카메라에 덜 따라 움직이며 뒤에 그려진다. 비우면 mid. 맨 위 오브젝트만 가진다(밑의 것은 부모를 따른다)");
        OnCommit(_rigNodeDepth, text => WithOpenCut(cut =>
        {
            if (EditedRigNode is { } node)
            {
                string trimmed = text.Trim();

                if (trimmed.Length == 0)
                {
                    _session.Editor.SetCutNodeDepth(cut.Name, node.Id, null);
                }
                else if (TryFloat(trimmed, out float depth))
                {
                    _session.Editor.SetCutNodeDepth(cut.Name, node.Id, depth);
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

    private string NewCut()
    {
        string name = "cut";

        for (int i = 2; _session.Rig?.RigKinds.Contains(name) == true || _session.Editor.Document.FindRig(name) is not null || _session.Editor.Document.Find(name) is not null; i++)
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
            Write(_cutZoom, cut is null ? "" : F(cut.Camera.Zoom));
            Write(_cutPanX, cut is null ? "" : F(cut.Camera.PanX));
            Write(_cutPanY, cut is null ? "" : F(cut.Camera.PanY));

            // 깊이 칸 — 컷의 맨 위 오브젝트일 때만.
            RigNodeAsset? node = EditedRigNode;
            bool top = cut is not null && node is { Parent.Length: 0 };

            if (_rigNodeDepthRow is not null)
            {
                _rigNodeDepthRow.IsVisible = top;
            }

            Write(_rigNodeDepth, top && node!.Depth is { } depth ? F(depth) : "");
        }
        finally
        {
            _refreshDepth--;
        }
    }
}
