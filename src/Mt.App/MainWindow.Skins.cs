using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Sets;

namespace Mt.App;

// 스킨의 셸(PLAN-parts.md P1) — "오브젝트" 탭 아래, 그림 오브젝트가 있는 컷이나 세트를 열었을 때만 보인다.
// 스킨 = 그림 오브젝트(조각)마다 붙일 그림의 묶음에 이름을 붙인 것. 어느 스킨을 입혀 볼지는 보기 상태다(문서를 안 바꾼다) — 스킨의 조각 줄에서 그림을 정한다.
public partial class MainWindow
{
    private const string NoSkin = "(없음)";

    private readonly StackPanel _skinPanel = new() { Spacing = 4, IsVisible = false };
    private readonly ComboBox _skinCombo = new() { FontSize = 11, MinWidth = 120 };
    private readonly TextBox _skinName = new() { FontSize = 11 };
    private readonly StackPanel _skinParts = new() { Spacing = 3 };

    /// <summary>테스트 손잡이 — "스킨 추가".</summary>
    internal string? AddSkinFromForm() => AddSkin();

    /// <summary>테스트 손잡이 — 스킨 칸이 보이는가, 고른 스킨, 조각 줄 수.</summary>
    internal (bool Visible, string? Selected, int PartRows) SkinFormState
        => (_skinPanel.IsVisible, _skinCombo.SelectedItem as string is { } s && s != NoSkin ? s : null, _skinParts.Children.Count);

    /// <summary>테스트 손잡이 — 조각 줄의 경로 칸(조각 노드 id로 찾는다).</summary>
    internal TextBox? SkinPartBox(string node)
        => _skinParts.Children.OfType<Panel>().SelectMany(p => p.Children.OfType<TextBox>()).FirstOrDefault(b => Equals(b.Tag, node));

    private Control BuildSkinPanel()
    {
        _skinPanel.Children.Add(Header("스킨 (조각 그림)"));
        _skinPanel.Children.Add(Labeled("입은 스킨", _skinCombo));
        ToolTip.SetTip(_skinCombo, "연 컷/세트에 입혀 보는 스킨 — 그림 오브젝트마다 어떤 그림을 그릴지의 묶음. 그 컷/세트의 스킨만 보인다. 그림이 없는 오브젝트는 안 그린다. 뷰포트에서 보는 것만 바뀐다(문서는 그대로)");
        Button add = SmallButton("스킨 추가", (_, _) => Guard("스킨 추가", () => AddSkin()));
        ToolTip.SetTip(add, "연 컷/세트의 새 스킨(skin, skin_2…)을 만들어 입혀 본다 — 되돌리기 한 번. 이름은 아래 칸에서(한글도 된다)");
        _skinPanel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { add, SmallButton("스킨 삭제", (_, _) => Guard("스킨 삭제", RemoveSkin)) },
        });
        _skinPanel.Children.Add(Labeled("이름", _skinName));
        _skinPanel.Children.Add(_skinParts);

        _skinCombo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || _skinCombo.SelectedItem is not string choice || _viewport.SetView is null)
            {
                return;
            }

            // 입혀 보는 스킨은 보기 상태다(문서를 안 바꾼다).
            _viewport.SetSkin = choice == NoSkin ? null : choice;
            RefreshSkinPanel();
        };
        OnCommit(_skinName, text =>
        {
            if (!_refreshing && _viewport.SetSkin is { } current && text.Trim() != current)
            {
                Guard("스킨 이름", () => _session.Editor.RenameSkin(current, text));
            }
        });
        return _skinPanel;
    }

    /// <summary>연 컷/세트에 그림 오브젝트(조각)가 있을 때만 보인다.</summary>
    private void RefreshSkinPanel()
    {
        _refreshDepth++;

        try
        {
            string? kind = _viewport.SetView;
            IReadOnlyList<string> parts = kind is null ? Array.Empty<string>() : ObjectParts.Of(_session.Built, kind);
            _skinPanel.IsVisible = parts.Count > 0;
            _skinParts.Children.Clear();
            _skinSetupBoxes.Clear();

            if (kind is null || parts.Count == 0)
            {
                return;
            }

            // 스킨이 하나라도 있으면 그 가운데 하나를 입혀 본다(뷰포트가 첫 스킨을 고른다) — 없을 때만 "(없음)".
            List<string> choices = _session.Editor.Document.Skins.Where(s => s.Set == kind).Select(s => s.Name).ToList();

            if (choices.Count == 0)
            {
                choices.Add(NoSkin);
            }

            string? worn = _viewport.SetSkin;
            _skinCombo.ItemsSource = choices;
            _skinCombo.SelectedIndex = worn is null ? 0 : Math.Max(0, choices.IndexOf(worn));

            SkinAsset? skin = _session.Editor.Document.FindSkin(worn);
            _skinName.IsEnabled = skin is not null;

            if (!_skinName.IsFocused)
            {
                _skinName.Text = skin?.Name ?? "";
            }

            if (skin is null)
            {
                return;
            }

            foreach (string node in OwnParts(parts))
            {
                _skinParts.Children.Add(SkinPartRow(skin, node));
            }

            _skinParts.Children.AddRange(NestedPartNotes(parts));
        }
        finally
        {
            _refreshDepth--;
        }
    }

    /// <summary>조각 한 줄 — 노드 이름 · 그림 경로(문서 기준, 비우면 그 조각을 안 그린다) · 파일 고르기.</summary>
    private Control SkinPartRow(SkinAsset asset, string node)
    {
        string skin = asset.Name;
        var box = new TextBox { FontSize = 11, Text = string.Join(FrameSeparator, asset.ImagesOf(node)), Tag = node, PlaceholderText = "(그림 없음)" };
        ToolTip.SetTip(box, $"{node}에 그릴 그림 — 문서 기준 상대 경로. 비우면 이 조각은 안 그린다. 여러 장을 ' | '로 이어 적으면 그림 넘기기(아래 칸의 장당 시간마다 넘어간다)");
        OnCommit(box, text =>
        {
            if (!_refreshing)
            {
                Guard("조각 그림", () => _session.Editor.SetSkinPartImages(skin, node, text.Split('|')));
            }
        });

        Button pick = SmallButton("…", async (_, _) =>
        {
            try
            {
                string? path = await PickOpenAsync("조각 그림 PNG", new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } });

                if (path is not null)
                {
                    _session.SetSkinPart(skin, node, path);
                }
            }
            catch (Exception exception)
            {
                _session.SetStatus($"조각 그림 실패: {exception.Message}");
            }
        });

        var row = new DockPanel();

        // 그림이 있는 조각에만: 그림 넘기기(장당 시간 · 되풀이)와 놓임(자리 · 크기 · 회전).
        if (asset.Parts.ContainsKey(node))
        {
            Control setup = SkinSetupRow(asset, node);
            DockPanel.SetDock(setup, Dock.Bottom);
            row.Children.Add(setup);
        }

        DockPanel.SetDock(pick, Dock.Right);
        row.Children.Add(pick);
        var label = new TextBlock { Text = node, FontSize = 11, Width = 96, VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
        DockPanel.SetDock(label, Dock.Left);
        row.Children.Add(label);
        row.Children.Add(box);
        return row;
    }

    private const string FrameSeparator = " | ";

    /// <summary>테스트 손잡이 — 조각의 덧붙임 칸들(장당 초 · 되풀이 · X · Y · 크기 · 회전). 그림이 없는 조각이면 null.</summary>
    internal SkinSetupBoxSet? SkinSetupBoxes(string node)
        => _skinSetupBoxes.TryGetValue(node, out SkinSetupBoxSet? boxes) ? boxes : null;

    /// <summary>조각 한 줄의 덧붙임 칸들.</summary>
    internal sealed record SkinSetupBoxSet(TextBox Seconds, CheckBox Loop, TextBox X, TextBox Y, TextBox Scale, TextBox Rotation);

    private readonly Dictionary<string, SkinSetupBoxSet> _skinSetupBoxes = new(StringComparer.Ordinal);

    private Control SkinSetupRow(SkinAsset asset, string node)
    {
        string skin = asset.Name;
        SkinPartSetup now = asset.SetupOf(node);
        TextBox Box(string text, string hint, string tip)
        {
            var box = new TextBox { FontSize = 10, Text = text, PlaceholderText = hint, Width = 46, MinWidth = 0, Padding = new Avalonia.Thickness(3, 1) };
            ToolTip.SetTip(box, tip);
            return box;
        }

        TextBox seconds = Box(F(now.FrameSeconds), "초", "그림 넘기기 — 한 장이 보이는 시간(초). 장이 여럿일 때만 쓰인다");
        var loop = new CheckBox { Content = "되풀이", FontSize = 10, IsChecked = now.Loop, IsEnabled = now.Frames.Count > 0 };
        ToolTip.SetTip(loop, "마지막 장 다음에 첫 장으로 돌아간다. 끄면 마지막 장에서 멈춘다(점이 하나씩 찍히고 서는 말풍선)");
        seconds.IsEnabled = now.Frames.Count > 0;
        TextBox x = Box(F(now.X), "X", "놓임 — 조각 노드 안에서의 자리 X(px). 모션과는 따로다: 꽃 셋을 흩어 놓는 것은 여기서, 움직임은 모션에서");
        TextBox y = Box(F(now.Y), "Y", "놓임 — 자리 Y(px)");
        TextBox scale = Box(F(now.Scale), "크기", "놓임 — 크기(배). 1이 조각 사각형에 꼭 맞는 크기");
        TextBox rotation = Box(F(now.Rotation), "회전", "놓임 — 회전(도, 반시계가 +)");
        _skinSetupBoxes[node] = new SkinSetupBoxSet(seconds, loop, x, y, scale, rotation);

        void Commit()
        {
            if (_refreshing || _session.Editor.Document.FindSkin(skin) is not { } current || !current.Parts.ContainsKey(node))
            {
                return;
            }

            SkinPartSetup value = current.SetupOf(node).Clone();
            value.FrameSeconds = TryFloat(seconds.Text ?? "", out float s) ? s : value.FrameSeconds;
            value.Loop = loop.IsChecked == true;
            value.X = TryFloat(x.Text ?? "", out float vx) ? vx : value.X;
            value.Y = TryFloat(y.Text ?? "", out float vy) ? vy : value.Y;
            value.Scale = TryFloat(scale.Text ?? "", out float vs) ? vs : value.Scale;
            value.Rotation = TryFloat(rotation.Text ?? "", out float vr) ? vr : value.Rotation;
            Guard("조각 놓임", () => _session.Editor.SetSkinPartSetup(skin, node, value));
        }

        foreach (TextBox box in new[] { seconds, x, y, scale, rotation })
        {
            OnCommit(box, _ => Commit());
        }

        loop.IsCheckedChanged += (_, _) => Commit();

        // 인스펙터가 좁아 두 줄로: 그림 넘기기 / 놓임.
        return new StackPanel
        {
            Spacing = 2,
            Margin = new Avalonia.Thickness(12, 1, 0, 4),
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Small("장당"), seconds, Small("초"), loop } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Small("놓임"), x, y, scale, rotation } },
            },
        };
    }

    /// <summary>연 컷/세트의 새 스킨을 만들어 입혀 본다 — 되돌리기 한 번.</summary>
    private string? AddSkin()
    {
        if (_viewport.SetView is not { } set)
        {
            return null;
        }

        DocumentEditor editor = _session.Editor;
        string name = "skin";

        for (int n = 2; editor.Document.FindSkin(name) is not null; n++)
        {
            name = $"skin_{n}";
        }

        editor.AddSkin(name, set);
        _viewport.SetSkin = name;
        RefreshSkinPanel();
        return name;
    }

    private void RemoveSkin()
    {
        if (_viewport.SetSkin is { } skin)
        {
            _session.Editor.RemoveSkin(skin);
        }
    }

    /// <summary>그 세트의 제 조각 — 품은 세트의 조각(경로 이름 <c>인스턴스.이름</c>)은 뺀다. 그 그림은 품은 세트의 스킨에 있다.</summary>
    private static IEnumerable<string> OwnParts(IReadOnlyList<string> parts)
        => parts.Where(p => !p.Contains(Mt.Authoring.Model.SetCodec.PathSeparator, StringComparison.Ordinal));

    /// <summary>품은 세트의 조각을 알리는 줄 — 인스턴스마다 한 줄("그림 없음"으로 보이지 않게, 어디서 고치는지와 함께).</summary>
    private static IEnumerable<Control> NestedPartNotes(IReadOnlyList<string> parts)
        => parts.Where(p => p.Contains(Mt.Authoring.Model.SetCodec.PathSeparator, StringComparison.Ordinal))
            .GroupBy(p => p.Split(Mt.Authoring.Model.SetCodec.PathSeparator)[0])
            .Select(g => (Control)new TextBlock
            {
                Text = $"{g.Key} — 품은 세트의 조각 {g.Count()}개. 그림은 그 세트를 열어 그 스킨에서 고친다",
                FontSize = 10,
                Opacity = 0.6,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 2, 0, 0),
            });
}
