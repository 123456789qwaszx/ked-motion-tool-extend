using System.Globalization;
using Avalonia.Controls;
using Mt.Authoring.Model;

namespace Mt.App;

// 재생 블록의 셸(PLAN-set.md S10) — "타임라인" 탭 아래. 세트가 품은 세트의 애니메이션을 "언제 튼다"로 놓는다.
// 품은 세트가 없는 세트의 애니메이션에서는 칸이 숨는다. 컷 파일에는 구운 키로 나가므로 게임은 재생 블록을 모른다.
public partial class MainWindow
{
    private readonly StackPanel _playPanel = new() { Spacing = 4 };
    private readonly StackPanel _playRows = new() { Spacing = 3 };

    /// <summary>테스트 손잡이 — "재생 블록 추가 (지금 시각)".</summary>
    internal int? AddPlayBlockFromForm() => AddPlayBlock();

    /// <summary>테스트 손잡이 — 재생 블록 칸이 보이는가, 줄 수, 한 줄의 칸들(시각 · 인스턴스 · 애니메이션 · 반복).</summary>
    internal bool PlayPanelShown => _playPanel.IsVisible;

    internal int PlayRowCount => _playRows.Children.Count;

    /// <summary>테스트 손잡이 — 줄의 "떼기".</summary>
    internal void DetachPlayRow(int index) => ((Panel)_playRows.Children[index]).Children.OfType<Button>().First(b => b.Content as string == "떼기").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    internal (TextBox Time, ComboBox Node, ComboBox Timeline, TextBox Repeat) PlayRow(int index)
    {
        var row = (Panel)_playRows.Children[index];
        TextBox[] boxes = row.Children.OfType<TextBox>().ToArray();
        ComboBox[] combos = row.Children.OfType<ComboBox>().ToArray();
        return (boxes[0], combos[0], combos[1], boxes[1]);
    }

    private Control BuildPlayPanel()
    {
        _playPanel.Children.Add(Header("재생 블록"));
        _playPanel.Children.Add(_playRows);
        Button add = SmallButton("재생 블록 추가 (지금 시각)", (_, _) => Guard("재생 블록 추가", () => AddPlayBlock()));
        ToolTip.SetTip(add, "이 세트가 품은 세트의 애니메이션을 지금 시각부터 튼다 — 블록이 끝나면 마지막 모습이 다음 블록까지 남고, 첫 블록 앞은 가만히 선 모습이다. 같은 인스턴스의 블록끼리는 겹칠 수 없다. 품은 애니메이션을 고치면 여기도 따라온다(참조). 컷을 내보낼 때는 키로 구워져 나간다");
        _playPanel.Children.Add(add);
        return _playPanel;
    }

    /// <summary>그 세트가 품은 인스턴스들(오브젝트 이름 → 품은 세트).</summary>
    private List<ObjectAsset> InstancesOf(TimelineAsset timeline)
        => _session.Editor.Document.FindSetOrCut(timeline.Set)?.Objects.Where(n => !string.IsNullOrEmpty(n.Set)).ToList() ?? new List<ObjectAsset>();

    private List<string> TimelinesOfSet(string? set)
        => _session.Editor.Document.Timelines.Where(m => m.Set == set).Select(m => m.Name).ToList();

    private int? AddPlayBlock()
    {
        if (_graph.Timeline is not { } timeline)
        {
            return null;
        }

        float time = (float)Math.Clamp(_graph.CurrentTime, 0, timeline.Duration);

        // 지금 시각에 들어가는 첫 (인스턴스, 애니메이션) — 없으면 첫 짝으로 시도해 까닭을 알린다.
        (string Node, string Timeline)? fallback = null;

        foreach (ObjectAsset instance in InstancesOf(timeline))
        {
            foreach (string child in TimelinesOfSet(instance.Set))
            {
                fallback ??= (instance.Id, child);
                float length = _session.Editor.Document.Find(child)!.Duration;
                bool free = timeline.Plays.Where(p => p.Object == instance.Id).All(p =>
                    time + length <= p.At + 1e-4f || time >= p.At + (_session.Editor.Document.Find(p.Timeline)?.Duration ?? 0f) * Math.Max(1, p.Repeat) - 1e-4f);

                if (free && time + length <= timeline.Duration + 1e-4f)
                {
                    return _session.Editor.AddPlayBlock(timeline.Name, instance.Id, child, time);
                }
            }
        }

        if (fallback is not { } first)
        {
            throw new InvalidOperationException("품은 세트에 애니메이션이 없다 — 그 세트를 열어 애니메이션을 먼저 만든다.");
        }

        return _session.Editor.AddPlayBlock(timeline.Name, first.Node, first.Timeline, time);
    }

    private void RefreshPlayPanel()
    {
        TimelineAsset? timeline = _graph.Timeline;
        _playPanel.IsVisible = timeline is not null && (timeline.Plays.Count > 0 || InstancesOf(timeline).Count > 0);

        // 글을 치는 중인 줄을 갈아 끼우지 않는다.
        if (_playRows.Children.OfType<Panel>().Any(row => row.Children.OfType<TextBox>().Any(b => b.IsFocused)))
        {
            return;
        }

        _refreshDepth++;

        try
        {
            _playRows.Children.Clear();

            if (timeline is null)
            {
                return;
            }

            for (int i = 0; i < timeline.Plays.Count; i++)
            {
                _playRows.Children.Add(PlayRowOf(timeline, i, timeline.Plays[i]));
            }
        }
        finally
        {
            _refreshDepth--;
        }
    }

    /// <summary>재생 블록 한 줄 — 시각(초) · 인스턴스 · 애니메이션 · 반복 · ×. 칸마다 확정이 되돌리기 한 칸.</summary>
    private Control PlayRowOf(TimelineAsset timeline, int index, PlayBlockAsset play)
    {
        string timelineName = timeline.Name;
        List<ObjectAsset> instances = InstancesOf(timeline);
        var time = new TextBox { FontSize = 11, Width = 52, Text = play.At.ToString("0.###", CultureInfo.InvariantCulture) };
        var node = new ComboBox { FontSize = 11, MinWidth = 70, ItemsSource = instances.Select(n => n.Id).ToList(), SelectedItem = play.Object };
        var child = new ComboBox { FontSize = 11, MinWidth = 80, ItemsSource = TimelinesOfSet(instances.FirstOrDefault(n => n.Id == play.Object)?.Set), SelectedItem = play.Timeline };
        var repeat = new TextBox { FontSize = 11, Width = 34, Text = play.Repeat.ToString(CultureInfo.InvariantCulture) };
        ToolTip.SetTip(time, "시작 시각(초, 이 애니메이션 기준)");
        ToolTip.SetTip(node, "어느 품은 세트(인스턴스)를 트나");
        ToolTip.SetTip(child, "그 세트의 어느 애니메이션을 트나");
        ToolTip.SetTip(repeat, "되풀이 횟수 — 바퀴마다 처음부터 다시 튼다");

        void Commit(string? nodeId, string? childName)
        {
            if (_refreshing || nodeId is null || childName is null)
            {
                return;
            }

            float t = TryFloat(time.Text ?? "", out float parsed) ? parsed : play.At;
            int count = int.TryParse(repeat.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : play.Repeat;
            Guard("재생 블록", () => _session.Editor.UpdatePlayBlock(timelineName, index, nodeId, childName, t, count));
        }

        OnCommit(time, _ => Commit(node.SelectedItem as string, child.SelectedItem as string));
        OnCommit(repeat, _ => Commit(node.SelectedItem as string, child.SelectedItem as string));
        child.SelectionChanged += (_, _) => Commit(node.SelectedItem as string, child.SelectedItem as string);
        // 인스턴스를 바꾸면 그 세트의 첫 애니메이션으로 — 다른 세트의 애니메이션은 틀 수 없다.
        node.SelectionChanged += (_, _) =>
        {
            string? id = node.SelectedItem as string;
            Commit(id, TimelinesOfSet(instances.FirstOrDefault(i => i.Id == id)?.Set).FirstOrDefault());
        };

        Button remove = SmallButton("×", (_, _) => Guard("재생 블록 삭제", () => _session.Editor.RemovePlayBlock(timelineName, index)));
        Button detach = SmallButton("떼기", (_, _) => Guard("재생 블록 떼기", () =>
        {
            int count = _session.Editor.DetachPlayBlocks(timelineName, play.Object);
            _session.SetStatus($"{play.Object}: 재생 블록을 떼어 채널 {count}개를 이 애니메이션의 제 키로 굳혔다 — 이제 원본을 따르지 않는다.");
        }));
        ToolTip.SetTip(detach, "따로 떼기 — 이 인스턴스의 재생 블록들이 내던 움직임을 이 애니메이션의 제 키(경로 이름의 채널)로 굳히고 블록을 지운다. 재생은 그대로이고, 그 뒤로는 품은 애니메이션을 고쳐도 여기는 안 바뀐다(여기서만 다르게 고칠 때). 같은 인스턴스의 블록은 함께 떼진다");
        // 두 줄 — 윗줄: 시각 · 인스턴스 · 떼기 · × / 아랫줄: 애니메이션 · 반복. 옆 칸이 좁아 한 줄에는 이름이 잘린다.
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Avalonia.Thickness(0, 0, 0, 3) };

        void Place(Control control, int line, int column, int span = 1)
        {
            Grid.SetRow(control, line);
            Grid.SetColumn(control, column);
            Grid.SetColumnSpan(control, span);
            row.Children.Add(control);
        }

        node.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        child.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        Place(time, 0, 0);
        Place(node, 0, 1);
        Place(detach, 0, 2);
        Place(remove, 0, 3);
        Place(child, 1, 0, 2);
        Place(repeat, 1, 2, 2);
        return row;
    }
}
