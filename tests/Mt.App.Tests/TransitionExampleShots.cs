using Avalonia.Headless;
using Mt.App;

namespace Mt.App.Tests;

/// <summary>
/// 화면 이동 예시(<c>examples/transitions/station</c>)를 앱에서 열어 장면을 그림으로 뽑는다 — <c>MT_RENDER_DIR=폴더 dotnet test --filter TransitionExampleShots</c>.
/// 폴더가 없으면 아무것도 하지 않는다(보통의 테스트 실행).
/// </summary>
public class TransitionExampleShots
{
    [Fact]
    public void 화면_이동_예시의_수명을_시간별로_뽑는다()
    {
        if (RenderShots.Directory is not { } directory)
        {
            return;
        }

        HeadlessUi.Run(() =>
        {
            string settings = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"settings-{Guid.NewGuid():N}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
            File.WriteAllText(settings, "{}");
            var window = new MainWindow(settings) { Width = 1500, Height = 950 };
            window.Show();

            try
            {
                DirectoryInfo? root = new(AppContext.BaseDirectory);

                while (root is not null && !File.Exists(Path.Combine(root.FullName, "KedMotionTool.sln")))
                {
                    root = root.Parent;
                }

                window.Session.Open(Path.Combine(root!.FullName, "examples", "transitions", "station", "station.motion-authoring.json"));
                Directory.CreateDirectory(directory);

                void Shot(string name)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using FileStream stream = File.Create(Path.Combine(directory, name + ".png"));
                    window.CaptureRenderedFrame()?.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                }

                // 연출을 본다 — 씬에 세트가 없어도 보기 전용 오브젝트가 대신 선다. 캐릭터(자리표시)는 그 뒤에 있다.
                window.InspectorTab = 4;
                window.SelectEmoji("station");

                foreach (double t in new[] { 0.1, 0.3, 0.6, 0.9, 1.2, 1.7, 2.5, 2.8, 3.05 })
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    window.Viewport.ScrubTo(t);
                    Shot($"station-t{t:0.00}");
                }

                // 세트를 연다 — 배치 모드.
                window.SelectSet("station");
                window.InspectorTab = 1;
                Shot("station-set");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
