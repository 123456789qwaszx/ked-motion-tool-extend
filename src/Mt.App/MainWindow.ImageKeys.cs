using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Mt.Authoring.Model;

namespace Mt.App;

// 그림 키의 셸(PLAN-standalone.md C4) — "타임라인" 탭 아래. 계단 키: 그 프레임부터 그 오브젝트의 그림이 바뀐다(표정 바꾸기).
// 그림을 그리는 오브젝트를 고르고 "그림 키 추가 (지금 프레임)…"로 그림 파일을 고른다. 컷의 타임라인이면 시각을 프레임으로 보인다.
public partial class MainWindow
{
    private readonly StackPanel _imageKeyPanel = new() { Spacing = 4 };
    private readonly StackPanel _imageKeyRows = new() { Spacing = 3 };

    /// <summary>테스트 손잡이 — 그림 키 칸이 보이는가, 줄 수, "그림 키 추가"(파일 고르기 뒤의 일).</summary>
    internal bool ImageKeyPanelShown => _imageKeyPanel.IsVisible;

    internal int ImageKeyRowCount => _imageKeyRows.Children.Count;

    internal int? AddImageKeyFromFile(string path) => AddImageKey(path);

    private Control BuildImageKeyPanel()
    {
        _imageKeyPanel.Children.Add(Header("그림 키"));
        _imageKeyPanel.Children.Add(_imageKeyRows);
        Button add = SmallButton("그림 키 추가 (지금 프레임)…", async (_, _) =>
        {
            try
            {
                if (ImageKeyTarget() is null)
                {
                    _session.SetStatus("그림 키: 하이어아키에서 그림을 그리는 오브젝트를 먼저 고른다.");
                    return;
                }

                if (await PickOpenAsync("그림 PNG", new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } }) is { } path)
                {
                    Guard("그림 키 추가", () => AddImageKey(path));
                }
            }
            catch (Exception exception)
            {
                _session.SetStatus($"그림 키 추가 실패: {exception.Message}");
            }
        });
        ToolTip.SetTip(add, "고른 오브젝트의 그림을 지금 프레임부터 바꾼다 — 표정 바꾸기처럼 사이값이 없는 계단 키다. 다음 그림 키까지 그 그림이 이어지고, 첫 그림 키 앞에서는 원래 그림(스킨의 것)이다. 같은 프레임에 다시 놓으면 그 키를 대신한다");
        _imageKeyPanel.Children.Add(add);
        return _imageKeyPanel;
    }

    /// <summary>그림 키를 놓을 오브젝트 — 연 세트·컷에서 고른, 그림을 그리는 오브젝트.</summary>
    private ObjectAsset? ImageKeyTarget() => _graph.Timeline is { } timeline && timeline.Set == SelectedSet && EditedObject is { Image: true } node ? node : null;

    private int? AddImageKey(string path)
    {
        if (_graph.Timeline is not { } timeline || ImageKeyTarget() is not { } node)
        {
            return null;
        }

        float time = (float)Math.Clamp(_graph.CurrentTime, 0, timeline.Duration);
        int index = _session.Editor.AddImageKey(timeline.Name, node.Id, time, _session.RelativeToDocument(path));
        RefreshAssets(); // 새 그림을 싣는다
        return index;
    }

    private void RefreshImageKeyPanel()
    {
        TimelineAsset? timeline = _graph.Timeline;
        // 툴이 정의한 세트·컷의 애니메이션에서만 — 그림을 그리는 오브젝트가 있을 때.
        _imageKeyPanel.IsVisible = timeline is not null && (timeline.ImageKeys.Count > 0 || _session.Editor.Document.FindSetOrCut(timeline.Set)?.Objects.Any(n => n.Image) == true);
        _imageKeyRows.Children.Clear();

        if (timeline is null)
        {
            return;
        }

        bool frames = _session.Editor.Document.FindCut(timeline.Set) is not null;

        for (int i = 0; i < timeline.ImageKeys.Count; i++)
        {
            ImageKeyAsset imageKey = timeline.ImageKeys[i];
            int index = i;
            string timelineName = timeline.Name;
            string when = frames
                ? $"{Math.Round(imageKey.T * CutAsset.Fps).ToString(CultureInfo.InvariantCulture)}f"
                : $"{imageKey.T.ToString("0.###", CultureInfo.InvariantCulture)}초";
            var text = new TextBlock
            {
                Text = $"{when}  {imageKey.Object}  →  {System.IO.Path.GetFileName(imageKey.Image)}",
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            };
            ToolTip.SetTip(text, imageKey.Image);
            Button remove = SmallButton("×", (_, _) => Guard("그림 키 삭제", () => _session.Editor.RemoveImageKey(timelineName, index)));
            var row = new DockPanel();
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(text);
            _imageKeyRows.Children.Add(row);
        }
    }
}
