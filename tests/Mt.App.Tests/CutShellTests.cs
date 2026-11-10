using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Ked.Presentation.Core;
using Mt.App.Views;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;

namespace Mt.App.Tests;

/// <summary>컷의 화면(<c>PLAN-standalone.md</c> §6 · C2) — 컷을 만들어 열고, 오브젝트를 바로 놓고, 깊이와 카메라가 뷰포트에 선다.</summary>
public class CutShellTests
{
    private static MainWindow NewWindow()
    {
        string path = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"settings-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return new(path);
    }

    private static void Type(MainWindow window, TextBox box, string text)
    {
        box.Focus();
        box.Text = text;
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
    }

    [Fact]
    public void 새_컷을_만들면_열리고_오브젝트를_바로_놓으며_이름과_길이를_칸에서_고친다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;

                // 새 컷 — 한 칸. 열리고(뷰포트·하이어아키가 그 컷을 본다) 세트 목록의 선택은 없다.
                long revision = editor.Revision;
                Assert.Equal("cut", window.NewCutFromForm());
                Assert.Equal(revision + 1, editor.Revision);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(("cut", "cut", "cut"), (window.SelectedCut, window.SelectedSet, view.SetView));
                Assert.NotNull(view.ShownCut);
                Assert.Equal(("cut", "240"), (window.CutBoxes.Name.Text, window.CutBoxes.Frames.Text));
                Assert.True(view.IsLayoutMode);

                // 오브젝트를 바로 놓는다 — 세트를 먼저 만들지 않는다.
                window.SelectSetNode(null);
                Assert.Equal("Color1", window.AddColorNodeFromForm());
                Assert.Equal("Color1", editor.Document.FindCut("cut")!.Nodes[0].Id);
                Assert.Empty(editor.Document.Rigs);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(view.CurrentPose()!.Contains("set/Color1"));

                // 길이와 이름을 칸에서 — 각각 한 칸. 이름을 바꿔도 그 컷이 열린 채다.
                Type(window, window.CutBoxes.Frames, "120");
                Assert.Equal((120, 5f), (editor.Document.FindCut("cut")!.Frames, editor.Document.Find("cut")!.Duration));
                Type(window, window.CutBoxes.Name, "arrival");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.NotNull(editor.Document.FindCut("arrival"));
                Assert.Equal(("arrival", "arrival"), (window.SelectedCut, view.SetView));

                // 세트를 열면 컷의 선택이 풀리고, 컷을 다시 고르면 세트의 선택이 풀린다 — 하나만 연다.
                window.NewSetFromForm();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal((null, "set"), (window.SelectedCut, window.SelectedSet));
                window.SelectSet("arrival");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(("arrival", "arrival"), (window.SelectedCut, window.SelectedSet));

                // 컷 삭제 — 한 칸. 되돌리면 돌아온다.
                revision = editor.Revision;
                window.RemoveCutFromForm();
                Assert.Equal(revision + 1, editor.Revision);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Null(window.SelectedCut);
                Assert.Null(view.SetView);
                editor.Undo();
                Assert.NotNull(editor.Document.FindCut("arrival"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 컷의_카메라를_밀면_가까운_오브젝트가_더_따라오고_깊이_칸은_맨_위_오브젝트에만_선다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                window.NewCutFromForm();
                window.SelectSetNode(null);
                window.AddColorNodeFromForm();   // Color1
                window.SelectSetNode(null);
                window.AddColorNodeFromForm();   // Color2
                window.SelectSetNode("Color2");
                window.AddColorNodeFromForm();   // Color3 — Color2 밑
                window.InspectorTab = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                float X(string id) => view.CurrentPose()!.TransformPoint("set/" + id, Vec3.Zero).X;
                float rest = X("Color1");

                // 깊이 칸 — 맨 위 오브젝트에서만 서고, 적으면 한 칸. 밑의 것에서는 숨는다.
                window.SelectSetNode("Color2");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(window.RigNodeDepthShown);
                long revision = editor.Revision;
                Type(window, window.RigNodeDepthBox, "20");
                Assert.Equal((20f, revision + 1), (editor.Document.FindCut("cut")!.Rig.Find("Color2")!.Depth, editor.Revision));
                window.SelectSetNode("Color3");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(window.RigNodeDepthShown);

                // 카메라를 100 민다(칸) — mid(Color1)는 100, close(Color2)는 120. 밑의 Color3은 부모를 따른다.
                Type(window, window.CutBoxes.PanX, "100");
                Assert.Equal(100f, editor.Document.FindCut("cut")!.Camera.PanX);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(100f, X("Color1") - rest, 1);
                Assert.Equal(120f, X("Color2") - rest, 1);
                Assert.Equal(120f, X("Color3") - rest, 1);

                // 카메라를 당긴 채(배율 1.2) 끌어도 끈 만큼 간다 — 자리는 그 오브젝트의 틀 안의 값으로 적힌다.
                Type(window, window.CutBoxes.PanX, "0");
                Type(window, window.CutBoxes.Zoom, "4");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Point At(Vec2 p) => view.TranslatePoint(view.ToScreen(p), window) ?? throw new InvalidOperationException("뷰가 창에 붙어 있지 않다");
                Vec2 grab = view.CurrentPose()!.TransformPoint("set/Color1", new Vec3(150f, 100f, 0f)).XY; // Color1의 귀퉁이 쪽(Color2에 안 가린 곳이 없으니 Color2를 옮겨 둔다)
                editor.UpdateRigNode("cut", "Color2", new RigNodeAsset { X = 700f, Y = 300f, Width = 400f, Height = 300f, Color = "#3D5A80", Alpha = true });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var drop = new Vec2(grab.X + 120f, grab.Y);
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal(100f, editor.Document.FindCut("cut")!.Rig.Find("Color1")!.X, 0); // 화면의 120 = 틀 안의 100(배율 1.2)
            }
            finally
            {
                window.Close();
            }
        });
    }
}
