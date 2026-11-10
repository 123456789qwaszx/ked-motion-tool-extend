using Avalonia.Headless;
using Mt.App;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;

namespace Mt.App.Tests;

/// <summary>
/// 화면을 그림으로 뽑는다 — <c>MT_RENDER_DIR=&lt;폴더&gt;</c>를 주면 헤드리스가 Skia로 실제 렌더해 PNG로 쓴다(안 주면 아무것도 안 한다).
/// 고친 화면을 눈으로 확인하는 길이다: 빈 창 · 컷을 열어 층을 들이고 깊이와 카메라를 준 모습.
/// </summary>
public class CutShots
{
    [Fact]
    public void 화면을_그림으로_뽑는다()
    {
        string? directory = Environment.GetEnvironmentVariable("MT_RENDER_DIR");

        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        HeadlessUi.Run(() =>
        {
            string settings = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"settings-{Guid.NewGuid():N}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
            File.WriteAllText(settings, "{}");
            var window = new MainWindow(settings);
            window.Show();

            try
            {
                Directory.CreateDirectory(directory);

                void Shot(string name)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using FileStream stream = File.Create(Path.Combine(directory, name + ".png"));
                    window.CaptureRenderedFrame()?.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                }

                // 빈 창 — 아무것도 안 열었다.
                Shot("empty");

                // 새 컷에 층 표를 들이고, 층마다 깊이를 주고, 카메라에 키를 놓는다.
                window.NewCutFromForm();
                window.ImportLayerSheetFromForm(TestDocuments.ArtPath("layers.tsv"));
                MotionEditor editor = window.Session.Editor;
                editor.SetCutNodeDepth("cut", "sign", 6f);
                editor.SetCutNodeDepth("cut", "microbe_sun", 20f);
                editor.SetCutCameraKey("cut", 0f, new CameraSettings());
                editor.SetCutCameraKey("cut", 4f, new CameraSettings { PanX = -200f, Zoom = 3f });
                window.SelectSetNode("sign");
                window.InspectorTab = 1;
                Shot("cut-object-tab");

                window.Graph.CurrentTime = 4.0;
                window.InspectorTab = 2;
                Shot("cut-motion-tab");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
