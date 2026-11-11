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
    public void 새_창은_비어_있고_새_컷에서_오브젝트와_키를_놓아_컷_파일로_낸다()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"start-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        // 옛 설정 파일 — 걷어 낸 설정(무대 뼈대 보기)이 들어 있어도 읽힌다.
        string settings = Path.Combine(folder, "settings.json");
        File.WriteAllText(settings, "{ \"showStageSkeleton\": true, \"inspectorTab\": 1 }");

        HeadlessUi.Run(() =>
        {
            var window = new MainWindow(settings);
            window.Show();

            try
            {
                DocumentEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 새 문서는 비어 있다 — 컷도 세트도 타임라인도 없고, 연 것이 없고, 뷰포트는 빈 무대다(포즈를 물어도 던지지 않는다).
                Assert.Null(window.Settings.LoadProblem);
                Assert.Equal(1, window.InspectorTab); // 옛 설정의 탭(1 = 지금의 "오브젝트")
                Assert.Empty(editor.Document.Cuts);
                Assert.Empty(editor.Document.Sets);
                Assert.Empty(editor.Document.Timelines);
                Assert.Empty(window.Session.Built.SetNames);
                Assert.Equal((null, null), (window.SelectedCut, window.SelectedSet));
                Assert.Null(view.SetView);
                Assert.Null(view.CurrentPose());
                Assert.False(view.HasSet);
                Assert.Empty(window.TimelineListState.Names);
                Assert.Equal(0, window.ProblemRowsShown);
                Assert.Throws<InvalidOperationException>(() => window.AddTimelineFromButton()); // 애니메이션은 연 세트(컷)의 것이다
                Assert.DoesNotContain("\"scenes\"", Mt.Authoring.Serialization.AuthoringDocumentJson.Write(editor.Document), StringComparison.Ordinal);

                // 새 컷 — 들어가는 문. 열리고 타임라인이 붙는다.
                Assert.Equal("cut", window.NewCutFromForm());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("cut", view.SetView);
                Assert.NotNull(view.CurrentPose());

                // 오브젝트 하나, 그리고 그 자리의 키 둘(0프레임 · 24프레임).
                window.SelectObject(null);
                Assert.Equal("Color1", window.AddColorNodeFromForm());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                view.SetComponent("Color1", ChannelKind.Position, Axis.X, -200f);
                view.SetKey();
                view.ScrubTo(1.0);
                view.SetComponent("Color1", ChannelKind.Position, Axis.X, 300f);
                view.SetKey();
                Assert.Equal(new[] { (0f, -200f), (1f, 300f) }, editor.Document.Find("cut")!.FindChannel("Color1", ChannelKind.Position)!.X!.Keys.Select(k => (k.T, k.V)));
                view.ScrubTo(0.5);
                Assert.InRange(view.CurrentPose()!.GetState("set/Color1").AnchoredPosition.X, -200f, 300f);

                // 컷 내보내기 — 컷 파일(cut/1)이 서고 코어가 읽는다.
                string exported = Path.Combine(folder, "cut.cut.json");
                Assert.True(window.ExportCutToFile(exported), window.Session.Status);
                Ked.Presentation.Core.CutFileDto file = Mt.Authoring.Serialization.CutFileJson.Read(File.ReadAllText(exported));
                Assert.Equal(("cut/1", "cut", 240), (file.format, file.name, file.frames));
                Assert.Equal("Color1", Assert.Single(file.objects).id);
                Ked.Presentation.Core.CutTrackDto track = file.tracks.Single(t => t.target == "Color1" && t.what == "x");
                Assert.Equal(new[] { (0f, -200f), (24f, 300f) }, track.keys.Select(k => (k.f, k.v)));
            }
            finally
            {
                window.Close();
            }
        });

        Directory.Delete(folder, recursive: true);
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
                DocumentEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;

                // 새 컷 — 한 칸. 열리고(뷰포트·하이어아키가 그 컷을 본다) 세트 목록의 선택은 없다.
                long revision = editor.Revision;
                Assert.Equal("cut", window.NewCutFromForm());
                Assert.Equal(revision + 1, editor.Revision);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(("cut", "cut", "cut"), (window.SelectedCut, window.SelectedSet, view.SetView));
                Assert.NotNull(view.ShownCut);
                Assert.Equal(("cut", "240"), (window.CutBoxes.Name.Text, window.CutBoxes.Frames.Text));
                // 타임라인이 바로 붙는다 — 따로 고르지 않는다. 종료 처리 같은 칸은 컷에서 고를 것이 없다.
                Assert.Equal("cut", view.Timeline?.Name);
                Assert.Equal("cut", window.Graph.Timeline?.Name);

                // 오브젝트를 바로 놓는다 — 세트를 먼저 만들지 않는다.
                window.SelectObject(null);
                Assert.Equal("Color1", window.AddColorNodeFromForm());
                Assert.Equal("Color1", editor.Document.FindCut("cut")!.Objects[0].Id);
                Assert.Empty(editor.Document.Sets);
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
                DocumentEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                window.NewCutFromForm();
                window.SelectObject(null);
                window.AddColorNodeFromForm();   // Color1
                window.SelectObject(null);
                window.AddColorNodeFromForm();   // Color2
                window.SelectObject("Color2");
                window.AddColorNodeFromForm();   // Color3 — Color2 밑
                window.InspectorTab = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                float X(string id) => view.CurrentPose()!.TransformPoint("set/" + id, Vec3.Zero).X;
                float rest = X("Color1");

                // 깊이 칸 — 맨 위 오브젝트에서만 서고, 적으면 한 칸. 밑의 것에서는 숨는다.
                window.SelectObject("Color2");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(window.ObjectDepthShown);
                long revision = editor.Revision;
                Type(window, window.ObjectDepthBox, "20");
                Assert.Equal((20f, revision + 1), (editor.Document.FindCut("cut")!.AsSet.Find("Color2")!.Depth, editor.Revision));
                window.SelectObject("Color3");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(window.ObjectDepthShown);

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
                editor.UpdateObject("cut", "Color2", new ObjectAsset { X = 700f, Y = 300f, Width = 400f, Height = 300f, Color = "#3D5A80", Alpha = true });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var drop = new Vec2(grab.X + 120f, grab.Y);
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal(100f, editor.Document.FindCut("cut")!.AsSet.Find("Color1")!.X, 0); // 화면의 120 = 틀 안의 100(배율 1.2)
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 컷에서_키_없는_오브젝트는_끌면_가만히_선_자리가_바뀌고_카메라는_칸과_키_단추로_타임라인에_놓인다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                DocumentEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                window.NewCutFromForm();
                window.SelectObject(null);
                window.AddColorNodeFromForm(); // Color1 — 400×300, 가운데
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Point At(Vec2 p) => view.TranslatePoint(view.ToScreen(p), window) ?? throw new InvalidOperationException("뷰가 창에 붙어 있지 않다");
                ObjectAsset Color() => editor.Document.FindCut("cut")!.AsSet.Find("Color1")!;
                TimelineAsset Timeline() => editor.Document.Find("cut")!;

                // 타임라인이 붙어 있어도, 자리에 키가 없는 오브젝트는 끌면 가만히 선 자리가 바뀐다(키가 안 생긴다). 한 칸.
                long revision = editor.Revision;
                Vec2 grab = view.CurrentPose()!.TransformPoint("set/Color1", Vec3.Zero).XY;
                var drop = new Vec2(grab.X + 60f, grab.Y + 30f);
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal((60f, 30f), (Color().X, Color().Y));
                Assert.Empty(Timeline().Channels);
                Assert.Equal(revision + 1, editor.Revision);

                // 자리에 키가 생기면 끌어 놓은 자리가 지금 프레임의 키가 된다(한 칸) — 가만히 선 자리는 그대로다.
                editor.AddChannel("cut", "Color1", ChannelKind.Position);
                editor.AddKey("cut", "Color1", ChannelKind.Position, Axis.X, new KeyAsset { T = 0f, V = 60f });
                window.Graph.CurrentTime = 1.0;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                grab = view.CurrentPose()!.TransformPoint("set/Color1", Vec3.Zero).XY;
                drop = new Vec2(grab.X + 40f, grab.Y + 10f);
                revision = editor.Revision;
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal((60f, 30f), (Color().X, Color().Y));
                Assert.Equal("Color1", view.SelectedNode);
                ChannelAsset moved = Timeline().FindChannel("Color1", ChannelKind.Position)!;
                Assert.Equal(new[] { (0f, 60f), (1f, 100f) }, moved.X!.Keys.Select(k => (k.T, k.V)));
                Assert.Equal(new[] { (1f, 40f) }, moved.Y!.Keys.Select(k => (k.T, k.V))); // 끌 때의 y(가만히 선 30) + 10 — y에도 키가 선다

                // 누르기만 하고 안 끌면 키가 안 생긴다.
                revision = editor.Revision;
                grab = view.CurrentPose()!.TransformPoint("set/Color1", Vec3.Zero).XY;
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseUp(At(grab), MouseButton.Left);
                Assert.Equal(revision, editor.Revision);
                window.Graph.CurrentTime = 0.0;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 카메라 — 키가 없는 동안 칸은 가만히 선 값을 고친다.
                Type(window, window.CutBoxes.PanX, "50");
                Assert.Equal(50f, editor.Document.FindCut("cut")!.Camera.PanX);
                Assert.False(editor.CutCameraIsKeyed("cut"));

                // "키" — 지금 프레임(0)에 카메라의 키가 놓인다(한 칸). 그 뒤로 칸을 고치면 그 프레임의 키가 된다.
                revision = editor.Revision;
                window.KeyCutCameraFromForm();
                Assert.Equal(revision + 1, editor.Revision);
                Assert.True(editor.CutCameraIsKeyed("cut"));
                window.Graph.CurrentTime = 2.0;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Type(window, window.CutBoxes.PanX, "250");
                Assert.Equal(50f, editor.Document.FindCut("cut")!.Camera.PanX); // 가만히 선 값은 그대로
                Assert.Equal(new[] { (0f, 50f), (2f, 250f) }, Timeline().FindChannel("Camera_Pan", ChannelKind.Position)!.X!.Keys.Select(k => (k.T, k.V)));
                Assert.Equal(0, window.ProblemRowsShown);

                // 칸은 지금 프레임의 값을 보이고, 뷰포트의 카메라가 타임라인을 따른다(mid의 오브젝트가 밀기만큼 움직인다).
                float X(double t)
                {
                    window.Graph.CurrentTime = t;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    return view.CurrentPose()!.TransformPoint("set/Color1", Vec3.Zero).X;
                }

                Assert.Equal(240f, X(2.0) - X(0.0), 1); // 카메라 200 + 오브젝트의 제 키 40(0초 60 → 1초 100)
                window.Graph.CurrentTime = 0.0;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                view.Focus();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("50", window.CutBoxes.PanX.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 그림_키를_놓으면_그_프레임부터_오브젝트의_그림이_바뀐다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                DocumentEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                window.NewCutFromForm();
                window.ImportLayerSheetFromForm(TestDocuments.ArtPath("layers.tsv"));
                window.InspectorTab = 2; // 타임라인 탭
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(window.ImageKeyPanelShown);
                string original = view.PartImage("sign")!;
                Assert.EndsWith("06_sign.png", original, StringComparison.Ordinal);

                // 오브젝트를 안 골랐으면 놓이지 않는다.
                window.SelectObject(null);
                Assert.Null(window.AddImageKeyFromFile(TestDocuments.ArtPath("08_microbe_sun.png")));

                // sign을 고르고 48프레임(2초)에 그림 키 — 한 칸, 줄이 선다.
                window.SelectObject("sign");
                window.Graph.CurrentTime = 2.0;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                long revision = editor.Revision;
                Assert.Equal(0, window.AddImageKeyFromFile(TestDocuments.ArtPath("08_microbe_sun.png")));
                Assert.Equal(revision + 1, editor.Revision);
                ImageKeyAsset imageKey = Assert.Single(editor.Document.Find("cut")!.ImageKeys);
                Assert.Equal(("sign", 2f), (imageKey.Object, imageKey.T));
                Assert.EndsWith("08_microbe_sun.png", imageKey.Image, StringComparison.Ordinal);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, window.ImageKeyRowCount);

                // 뷰포트 — 키 앞에서는 원래 그림, 키부터는 바뀐 그림(사이값이 없다). 그 그림이 실려 있다.
                string At(double t)
                {
                    window.Graph.CurrentTime = t;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    return view.PartImage("sign")!;
                }

                Assert.Equal(original, At(1.9));
                Assert.EndsWith("08_microbe_sun.png", At(2.0), StringComparison.Ordinal);
                Assert.EndsWith("08_microbe_sun.png", At(9.0), StringComparison.Ordinal);
                Assert.All(window.Session.SkinImages(), image => Assert.NotNull(image.Path));
                Assert.Equal(0, window.ProblemRowsShown);

                // 컷 내보내기 — 게임이 읽는 파일. 다른 폴더에 내도 그림 경로가 그 파일 기준으로 적혀 그림을 찾는다. 문서는 안 바뀐다.
                string folder = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"cut-{Guid.NewGuid():N}");
                Directory.CreateDirectory(folder);
                string exported = Path.Combine(folder, "cut.cut.json");
                revision = editor.Revision;
                Assert.True(window.ExportCutToFile(exported));
                Assert.Equal(revision, editor.Revision);
                Ked.Presentation.Core.CutFileDto file = Mt.Authoring.Serialization.CutFileJson.Read(File.ReadAllText(exported));
                Assert.Equal(("cut", 240), (file.name, file.frames));
                Assert.All(file.objects.Where(o => o.image is not null), o => Assert.True(File.Exists(Path.Combine(folder, o.image)), o.image));
                Assert.Equal(48f, file.images.Single(t => t.target == "sign").keys[0].f);
                Assert.True(File.Exists(Path.Combine(folder, file.images.Single(t => t.target == "sign").keys[0].v)));

                // 코어의 재생기가 그 파일을 그대로 읽는다 — 47프레임에는 원래 그림, 48프레임부터 바뀐 그림.
                Assert.EndsWith("06_sign.png", Ked.Presentation.Core.CutPlayer.Evaluate(file, 47f, null).Image["sign"], StringComparison.Ordinal);
                Assert.EndsWith("08_microbe_sun.png", Ked.Presentation.Core.CutPlayer.Evaluate(file, 48f, null).Image["sign"], StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
