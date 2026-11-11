using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Ked.Presentation.Core;
using Mt.Authoring.Graph;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;

namespace Mt.App.Tests;

public class MainWindowTests
{
    /// <summary>테스트는 이 컴퓨터의 설정 파일을 건드리지 않는다 — 임시 경로.</summary>
    private static MainWindow NewWindow()
    {
        string path = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"settings-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"snapStep\": 0.016666666666666666 }"); // 이 테스트들의 기대값은 1/60 스냅 기준 — 기본값은 1/24(GraphMathTests)
        return new(path);
    }

    [Fact]
    public void 주_창이_헤드리스에서_열리고_그려진다()
    {
        HeadlessUi.Run(() =>
        {
            Assert.True(HeadlessUi.OnUiThread);

            MainWindow window = NewWindow();
            window.Show();

            try
            {
                Assert.Equal("ked-motion-tool", window.Title);

                var status = window.FindControl<TextBlock>("StatusText");
                Assert.NotNull(status);
                Assert.True(status!.Bounds.Width > 0, "상태 줄이 배치되지 않았다");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>끝에서 끝까지: 빈 세트에서 폼·채널 박스·그래프 명령만으로 nod를 처음부터 만들고, 컷에 품어 틀어, 내보낸 컷 파일을 게임이 읽는 길로 읽으면 그 키가 있다.</summary>
    [Fact]
    public void 세트에서_nod를_처음부터_만들어_컷에_품어_내보내면_컷_파일에_그_키가_있다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();
            string export = Path.Combine(Path.GetTempPath(), $"ked-motion-tool-{Guid.NewGuid():N}.cut.json");

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");
                Assert.Equal("nod", window.SelectedTimeline);
                Assert.Equal("nod", window.Graph.TimelineName);

                window.AddChannelFromForm("CharacterPortrait_Track_Move_Y", ChannelKind.Position);
                Assert.Empty(window.Graph.Curves); // 성분은 키가 생기면 생긴다

                var y = new CurveRef("CharacterPortrait_Track_Move_Y", ChannelKind.Position, Axis.Y);
                window.Graph.CurrentTime = 0;
                FirstKey(window, y);
                Assert.Single(window.Graph.Curves);

                window.Graph.CurrentTime = 0.3;
                window.Graph.InsertKeyAtCurrentTime();
                window.CommitKeyValueFromInspector("-18");

                window.Graph.CurrentTime = 0.6;
                window.Graph.InsertKeyAtCurrentTime();
                window.CommitKeyValueFromInspector("0");

                window.Graph.SelectAll();
                window.Graph.SetTangentMode(TangentMode.Flat);
                Assert.Empty(window.ProblemsShown);

                // 컷을 만들어 그 세트를 품고, nod를 처음부터 튼다 — 컷 파일로 낸다.
                Mt.Authoring.Editing.DocumentEditor editor = window.Session.Editor;
                Assert.Equal("cut", window.NewCutFromForm());
                editor.AddInstance("cut", "hero", TestDocuments.Actor);
                editor.AddPlayBlock("cut", "hero", "nod", 0f);
                Assert.Empty(window.ProblemsShown);
                Assert.True(window.ExportCutToFile(export), window.Session.Status);

                // 게임이 읽는 모양 그대로 읽는다 — 품은 세트는 오브젝트로 펼쳐지고, nod의 키는 그 오브젝트의 y 트랙으로 구워져 있다(프레임, 평평한 기울기). 끝까지 마지막 값이 남는다.
                byte[] bytes = File.ReadAllBytes(export);
                Assert.NotEqual(0xEF, bytes[0]); // BOM이 없다
                CutFileDto file = CutFileJson.Read(System.Text.Encoding.UTF8.GetString(bytes));
                Assert.Equal(("cut/1", "cut", 240), (file.format, file.name, file.frames));
                Assert.Equal(26, file.objects.Count); // hero + actor의 오브젝트 25
                Assert.Contains(file.objects, o => o.id == "hero.CharacterPortrait_Track_Move_Y" && o.parent == "hero.CharacterPortrait_Track_Move_X");
                CutTrackDto track = Assert.Single(file.tracks);
                Assert.Equal(("hero.CharacterPortrait_Track_Move_Y", "y"), (track.target, track.what));
                Assert.Equal(new[] { (0f, 0f), (7.2f, -18f), (14.4f, 0f), (240f, 0f) }, track.keys.Select(k => (MathF.Round(k.f, 3), k.v)));
                Assert.All(track.keys, k => Assert.Equal((0f, 0f), (k.i, k.o)));

                // 코어의 재생도 그 모습이다.
                Assert.Equal(-18f, CutPlayer.Evaluate(file, 7.2f, null!).Tree.GetState("hero.CharacterPortrait_Track_Move_Y").AnchoredPosition.Y, 3);
            }
            finally
            {
                window.Close();
                File.Delete(export);
            }
        });
    }

    /// <summary>채널 박스의 ● — 고른 오브젝트의 그 성분을 지금 시각에 키한다(성분이 생기는 화면의 길).</summary>
    private static void FirstKey(MainWindow window, CurveRef curve)
    {
        window.Viewport.SelectedNode = curve.Node;
        window.KeyChannelBoxComponent(curve.Kind, curve.Axis);
    }

    [Fact]
    public void 뷰포트와_그래프의_현재_시각은_하나이고_세트를_열면_뷰포트에_선다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");

                Assert.Equal("nod", window.Viewport.TimelineName);
                Assert.True(window.Viewport.HasSet);
                Assert.NotNull(window.Viewport.ShownQuad());

                window.Viewport.ScrubTo(0.25);
                Assert.Equal(0.25, window.Graph.CurrentTime, 9);

                window.Graph.CurrentTime = 0.5;
                Assert.Equal(0.5, window.Viewport.Time, 9);

                // 길이 밖은 뷰포트가 길이로 죈다 — 그래프는 그대로.
                window.Graph.CurrentTime = 2;
                Assert.Equal(0.6f, window.Viewport.Time, 6); // 길이는 float(0.6f)다
                Assert.Equal(2, window.Graph.CurrentTime, 9);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 그래프와_도프시트는_선택이_하나이고_현재_시각도_하나다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");
                var y = new CurveRef("CharacterPortrait_Track_Move_Y", ChannelKind.Position, Axis.Y);
                window.AddChannelFromForm(y.Node, y.Kind);
                FirstKey(window, y);

                Assert.Same(window.Graph.Selection, window.Dope.Selection);
                Assert.Same(window.SharedSelection, window.Graph.Selection);
                Assert.Equal("nod", window.Dope.TimelineName);
                Assert.Equal(3, window.Dope.Rows.Count);

                window.Graph.Select(new[] { new KeyRef(y, 0) }, additive: false);
                Assert.Single(window.Dope.Selection);

                // 클립보드도 하나다 — 그래프에서 복사해 도프시트에서 붙인다.
                window.Graph.CopySelection();
                window.Dope.CurrentTime = 0.25;
                window.Dope.Paste();
                Assert.Equal(2, y.Resolve(window.Session.Editor.Document.Find("nod")!)!.Keys.Count);

                window.Dope.Selection.Clear();
                Assert.Empty(window.Graph.Selection);

                window.Dope.CurrentTime = 0.2;
                Assert.Equal(0.2, window.Graph.CurrentTime, 9);
                Assert.Equal(0.2, window.Viewport.Time, 9);

                window.Viewport.ScrubTo(0.4);
                Assert.Equal(0.4, window.Dope.CurrentTime, 9);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 뷰포트_드래그만으로_키가_생기고_노드_목록이_선다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("wave", TestDocuments.Actor, "0.5");
                Assert.Contains("CharacterPortrait_Track_Move_X", window.Viewport.NodeChoices);
                Assert.DoesNotContain("__root", window.Viewport.NodeChoices);

                window.Viewport.SelectedNode = "CharacterPortrait_Track_Move_X";
                window.Viewport.Manipulator = ManipulatorMode.Move;
                window.AutoKeyChecked = true;
                Assert.True(window.Viewport.AutoKey);

                window.Viewport.ScrubTo(0.25);
                Point center = window.Viewport.TranslatePoint(window.Viewport.GizmoCenter()!.Value, window)!.Value;
                var target = new Point(center.X + 40, center.Y);
                window.MouseDown(center, MouseButton.Left);
                window.MouseMove(target, RawInputModifiers.LeftMouseButton);
                window.MouseUp(target, MouseButton.Left);

                ChannelAsset channel = window.Session.Editor.Document.Find("wave")!.FindChannel("CharacterPortrait_Track_Move_X", ChannelKind.Position)!;
                Assert.Single(channel.X!.Keys);
                Assert.True(channel.X.Keys[0].V > 0);
                Assert.Equal(3, window.Dope.Rows.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 문제_행의_돕는_손이_문제를_없애고_설정은_파일로_남고_자동_저장은_문서_옆이다()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string settingsPath = Path.Combine(directory, "settings.json");

        HeadlessUi.Run(() =>
        {
            var window = new MainWindow(settingsPath);
            window.Show();

            try
            {
                // 이름이 규칙 밖인 애니메이션이 든 문서(손으로 쓴 것)를 연다 — 문제 행이 서고, 돕는 손 "이름 고치기"가 그것을 없앤다.
                string opened = Path.Combine(directory, "hand.cut-authoring.json");
                File.WriteAllText(opened, AuthoringDocumentJson.Write(TestDocuments.Nod().Document).Replace("\"name\": \"nod\"", "\"name\": \"Nod\"", StringComparison.Ordinal));
                window.OpenDocument(opened);
                Assert.True(window.ProblemRowsShown >= 1);
                Assert.True(window.ApplyFix("Nod", Mt.Authoring.Validation.ValidationFix.NameToValid));
                Assert.Equal(0, window.ProblemRowsShown);
                Assert.NotNull(window.Session.Editor.Document.Find("nod"));

                // 설정: 스냅을 끄고 창을 닫으면 파일에 남는다.
                window.Settings.SnapEnabled = false;
                string documentPath = Path.Combine(directory, "doc.cut-authoring.json");
                window.Session.Save(documentPath);
                window.Session.Editor.SetDuration("nod", 0.7f);
                string? autosaved = window.Session.AutoSave();
                Assert.NotNull(autosaved);
                Assert.Equal(Path.Combine(directory, "doc.autosave.cut-authoring.json"), autosaved);
                Assert.True(File.Exists(autosaved!));
                Assert.True(window.Session.IsDirty);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(File.Exists(settingsPath));
        Assert.Contains("\"snapEnabled\": false", File.ReadAllText(settingsPath), StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void 채널_박스는_고른_노드의_성분이고_값을_넣으면_그_노드에_키가_된다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                const string node = "CharacterPortrait_SwayPivot";
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("step", TestDocuments.Actor, "0.6");
                window.SelectObject(node);
                Assert.Equal(node, window.ChannelBoxNode);

                // 칸의 값은 포즈다 — 회전 0, 배율 1.
                Assert.Equal("0", window.ChannelBoxValue(ChannelKind.Euler, Axis.Z));
                Assert.Equal("1", window.ChannelBoxValue(ChannelKind.Scale, Axis.X));

                // Auto Key면 값을 넣는 순간 그 노드에 키 — 키 값은 넣은 값 그대로다.
                window.AutoKeyChecked = true;
                window.Viewport.ScrubTo(0.25);
                window.SetChannelBoxValue(ChannelKind.Euler, Axis.Z, "15");
                TimelineAsset step = window.Session.Editor.Document.Find("step")!;
                Assert.Equal(15.0, step.FindChannel(node, ChannelKind.Euler)!.Z!.Keys.Single().V, 3);
                Assert.Equal("15", window.ChannelBoxValue(ChannelKind.Euler, Axis.Z));

                // += 는 지금 값에 상대 — 같은 시각의 키가 바뀐다.
                window.SetChannelBoxValue(ChannelKind.Euler, Axis.Z, "+=5");
                step = window.Session.Editor.Document.Find("step")!;
                Assert.Equal(20.0, step.FindChannel(node, ChannelKind.Euler)!.Z!.Keys.Single().V, 3);

                // ● 는 지금 값으로 지금 시각에 키 — 채널이 없는 성분이면 채널이 생긴다. 되돌리기 한 번.
                window.Viewport.ScrubTo(0.5);
                long revision = window.Session.Editor.Revision;
                window.KeyChannelBoxComponent(ChannelKind.Position, Axis.X);
                Assert.Equal(revision + 1, window.Session.Editor.Revision);
                step = window.Session.Editor.Document.Find("step")!;
                KeyAsset key = step.FindChannel(node, ChannelKind.Position)!.X!.Keys.Single();
                Assert.Equal(0.5f, key.T, 3);
                Assert.Equal(0f, key.V, 3);
                window.Session.Editor.Undo();
                Assert.Null(window.Session.Editor.Document.Find("step")!.FindChannel(node, ChannelKind.Position));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 이름을_비우고_타임라인을_추가하면_timeline이_되고_규칙_밖_이름은_상태줄에_남는다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("", TestDocuments.Actor, "0.6");
                window.AddTimelineFromForm("  ", TestDocuments.Actor, "0.6");

                Assert.Equal(new[] { "timeline", "timeline_2" }, window.Session.Editor.Document.Timelines.Select(m => m.Name));
                Assert.DoesNotContain(window.ProblemsShown, p => p.Message.Contains("이름은", StringComparison.Ordinal));

                Assert.Throws<InvalidOperationException>(() => window.AddTimelineFromForm("Nod", TestDocuments.Actor, "0.6"));
                Assert.Equal(2, window.Session.Editor.Document.Timelines.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 아웃라이너는_타임라인의_채널을_노드로_보이고_흐린_성분_클릭은_만들지_않으며_S가_고른_노드를_키한다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");
                var y = new CurveRef("CharacterPortrait_Track_Move_Y", ChannelKind.Position, Axis.Y);
                var x = y with { Axis = Axis.X };
                window.AddChannelFromForm(y.Node, y.Kind);
                window.Session.Editor.SetKeys("nod", y.Node, y.Kind, y.Axis, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.3f, -18f), TestDocuments.Key(0.6f, 0f) });

                // 채널 하나 = 제목 줄 + 그 종류의 성분 전부(위치는 X·Y — 없는 것은 흐리게).
                Assert.Equal(3, window.OutlinerRowCount);
                Assert.Single(window.Graph.Curves);

                // 흐린 성분(X) 클릭은 성분을 만들지 않는다(마야식).
                window.OutlinerClick(x, additive: false);
                Assert.Single(window.Graph.Curves);
                Assert.Null(window.Session.Editor.Document.Find("nod")!.FindChannel(x.Node, x.Kind)!.X);

                // 있는 성분 클릭은 그 커브만 · 더하기.
                window.Session.Editor.SetKeys("nod", x.Node, x.Kind, x.Axis, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.6f, 0f) });
                Assert.Equal(2, window.Graph.Curves.Count);
                window.OutlinerClick(y, additive: false);
                Assert.True(window.Graph.Curves.Single(c => c.Curve == y).Visible);
                Assert.False(window.Graph.Curves.Single(c => c.Curve == x).Visible);
                window.OutlinerClick(x, additive: true);
                Assert.All(window.Graph.Curves, c => Assert.True(c.Visible));

                // 검색은 노드 이름·종류로 거른다.
                window.OutlinerQuery = "없는노드";
                Assert.Equal(0, window.OutlinerRowCount);
                window.OutlinerQuery = "Track_Move";
                Assert.Equal(3, window.OutlinerRowCount);
                window.OutlinerQuery = "";

                // S = 고른 노드의 있는 성분을 지금 값으로 현재 시각에 키 — 키 없는 포즈가 없어도. 되돌리기 한 번.
                window.SelectObject(y.Node);
                window.Viewport.ScrubTo(0.15);
                long revision = window.Session.Editor.Revision;
                window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.None);
                Assert.Equal(revision + 1, window.Session.Editor.Revision);
                ChannelAsset moveY = window.Session.Editor.Document.Find("nod")!.FindChannel(y.Node, y.Kind)!;
                Assert.Contains(moveY.Y!.Keys, k => Math.Abs(k.T - 0.15f) < 1e-4);
                Assert.Equal(4, moveY.Y.Keys.Count);
                window.Session.Editor.Undo();

                // 성분 하나 삭제는 다른 성분을 남기고, 마지막 성분이면 채널이 없어진다.
                window.RemoveComponentFromOutliner(x);
                moveY = window.Session.Editor.Document.Find("nod")!.FindChannel(y.Node, y.Kind)!;
                Assert.Null(moveY.X);
                Assert.NotNull(moveY.Y);
                window.RemoveComponentFromOutliner(y);
                Assert.Null(window.Session.Editor.Document.Find("nod")!.FindChannel(y.Node, y.Kind));

                // 시간 표시는 프레임이 기본: 0.5초 = 12프레임.
                window.Viewport.ScrubTo(0.5);
                Assert.Equal("12", window.CurrentTimeText);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 전역_단축키_이전_다음_키_프레임_복사_붙여넣기_지우기_인비트윈()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");
                var y = new CurveRef("CharacterPortrait_Track_Move_Y", ChannelKind.Position, Axis.Y);
                window.AddChannelFromForm(y.Node, y.Kind);
                window.Session.Editor.SetKeys("nod", y.Node, y.Kind, y.Axis, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.3f, -18f), TestDocuments.Key(0.6f, 0f) });
                TimelineAsset Nod() => window.Session.Editor.Document.Find("nod")!;

                // 2 다음 키 · 1 이전 키 (물리 키로 — 포커스가 어디 있든 듣는다)
                window.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.None);
                Assert.Equal(0.3, window.Viewport.Time, 4);
                window.NextKey();
                Assert.Equal(0.6, window.Viewport.Time, 4);
                window.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.None);
                Assert.Equal(0.3, window.Viewport.Time, 4);

                // Alt+2 / Alt+1 한 프레임(24fps)
                window.StepFrame(+1);
                Assert.Equal(0.3 + 1.0 / 24.0, window.Viewport.Time, 4);
                window.StepFrame(-1);
                Assert.Equal(0.3, window.Viewport.Time, 4);

                // Shift+W 복사 → 0.45로 가서 Shift+E 붙여넣기
                window.CopyKeysAtCurrentTime();
                window.Viewport.ScrubTo(0.45);
                window.PasteKeysAtCurrentTime();
                Assert.Equal(4, y.Resolve(Nod())!.Keys.Count);
                Assert.Equal(-18f, y.Resolve(Nod())!.Keys[2].V);

                // Shift+F 지우기
                window.ClearKeysAtCurrentTime();
                Assert.Equal(3, y.Resolve(Nod())!.Keys.Count);

                // Shift+2 인비트윈 넣기 / Shift+1 빼기 — 0.3 뒤의 키만 움직인다
                window.Viewport.ScrubTo(0.3);
                window.AddInbetween();
                Assert.Equal(0.6f + 1f / 24f, y.Resolve(Nod())!.Keys[2].T, 4);
                window.RemoveInbetween();
                Assert.Equal(0.6f, y.Resolve(Nod())!.Keys[2].T, 4);
                Assert.Equal(0.3f, y.Resolve(Nod())!.Keys[1].T, 4);

                // Alt+Q 재생 토글
                window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.Alt);
                Assert.True(window.Viewport.IsPlaying);
                window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.Alt);
                Assert.False(window.Viewport.IsPlaying);

                // ` 첫 프레임 · Alt+` 처음부터 재생(토글이 아니다 — 재생 중에 누르면 처음으로 되감는다) · Alt+Q 토글
                window.Viewport.ScrubTo(0.3);
                window.KeyPressQwerty(PhysicalKey.Backquote, RawInputModifiers.None);
                Assert.Equal(0, window.Viewport.Time, 6);
                window.Viewport.ScrubTo(0.3);
                window.KeyPressQwerty(PhysicalKey.Backquote, RawInputModifiers.Alt);
                Assert.True(window.Viewport.IsPlaying);
                Assert.InRange(window.Viewport.Time, 0, 0.15); // 처음부터(0.3이 아니라) — 재생 시계가 이미 한 틱 갔을 수 있다
                window.KeyPressQwerty(PhysicalKey.Backquote, RawInputModifiers.Alt);
                Assert.True(window.Viewport.IsPlaying);
                window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.Alt);
                Assert.False(window.Viewport.IsPlaying);

                // W/E/R/Q 매니퓰레이터 — 창 어디에 포커스가 있든.
                window.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.None);
                Assert.Equal(ManipulatorMode.Rotate, window.Manipulator);
                window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);
                Assert.Equal(ManipulatorMode.Scale, window.Manipulator);
                window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);
                Assert.Equal(ManipulatorMode.Move, window.Manipulator);
                window.KeyPressQwerty(PhysicalKey.Q, RawInputModifiers.None);
                Assert.Equal(ManipulatorMode.Select, window.Manipulator);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 되돌리기는_툴바_없이도_편집기를_지난다_그리고_문제는_아래에_보인다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("a", TestDocuments.Actor, "1");
                Assert.NotNull(window.Session.Editor.Document.Find("a"));

                // 채널이 없는 타임라인은 유효하다(채널은 첫 키에서 생긴다) — 문제 칸에 아무것도 없다. 길이를 0으로 하면 보인다.
                Assert.DoesNotContain(window.ProblemsShown, p => p.TimelineName == "a");
                window.Session.Editor.SetDuration("a", 0f);
                Assert.Contains(window.ProblemsShown, p => p.TimelineName == "a");
                window.Session.Editor.Undo();

                window.Session.Editor.Undo();
                Assert.Null(window.Session.Editor.Document.Find("a"));
                Assert.Null(window.Graph.TimelineName);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 인스펙터는_탭_셋이고_마지막_탭은_설정에_남는다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                Assert.Equal(3, window.InspectorTabCount); // 채널 박스 · 오브젝트 · 타임라인
                Assert.Equal(0, window.InspectorTab);
                window.InspectorTab = 2;
                Assert.Equal(2, window.Settings.InspectorTab);
                window.InspectorTab = 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    // 설정 창·단축키 창은 헤드리스 세션(창 하나 전제)에서 두 번째 창을 열면 UI 스레드가 멈춰 여기서 열지 않는다 — 데스크톱에서 확인한다.

    [Fact]
    public void 하이어아키는_연_세트의_부모자식_계층_그대로이고_Del로_고른_오브젝트를_지운다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                // 아무것도 안 열었으면 하이어아키가 비어 있다.
                Assert.Empty(window.HierarchyPathOf("CharacterPortrait_Track_Move_Y"));
                TestDocuments.OpenActor(window);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 맨 위 항목이 연 세트이고, 그 밑은 오브젝트의 부모 사슬이다.
                int depth = Array.IndexOf(TestDocuments.ActorChain, "CharacterPortrait_Track_Move_Y");
                Assert.Equal(new[] { $"세트  {TestDocuments.Actor}" }.Concat(TestDocuments.ActorChain.Take(depth + 1)), window.HierarchyPathOf("CharacterPortrait_Track_Move_Y"));

                // 고르면 채널 박스와 "오브젝트" 탭의 칸이 그것을 본다.
                window.SelectObject("CharacterPortraitSpriteOverlay_Image");
                Assert.Equal("CharacterPortraitSpriteOverlay_Image", window.ChannelBoxNode);
                Assert.Equal("CharacterPortraitSpriteOverlay_Image", window.SetEditorState.Node);

                // Del — 고른 오브젝트를 지운다, 한 칸.
                var editor = window.Session.Editor;
                long revision = editor.Revision;
                window.Hierarchy.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete, PhysicalKey = PhysicalKey.Delete }); // 하이어아키의 항목에 포커스가 있을 때의 Del
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Null(editor.Document.FindSetOrCut(TestDocuments.Actor)!.Find("CharacterPortraitSpriteOverlay_Image"));
                Assert.Empty(window.HierarchyPathOf("CharacterPortraitSpriteOverlay_Image"));
                editor.Undo();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(window.HierarchyPathOf("CharacterPortraitSpriteOverlay_Image"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Ctrl_Z_Y는_창_어디에_포커스가_있든_한_번만_돌고_글_입력_칸에서는_문서를_안_건드린다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");
                var editor = window.Session.Editor;

                foreach (Control focus in new Control[] { window.Graph, window.Dope, window.Viewport, window.Hierarchy })
                {
                    editor.SetDuration("nod", 1.5f);
                    long after = editor.Revision;
                    focus.Focus();

                    window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
                    Assert.Equal(0.6f, editor.Document.Find("nod")!.Duration);
                    Assert.Equal(after + 1, editor.Revision); // 정확히 한 번(두 번이면 +2)

                    window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
                    Assert.Equal(1.5f, editor.Document.Find("nod")!.Duration);
                    Assert.Equal(after + 2, editor.Revision);

                    window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
                    window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift); // Ctrl+Shift+Z = 다시하기
                    Assert.Equal(1.5f, editor.Document.Find("nod")!.Duration);
                    Assert.Equal(after + 4, editor.Revision);
                }

                // 글 입력 칸에 포커스가 있으면 Ctrl+Z는 글자 되돌리기 — 문서는 그대로.
                long revision = editor.Revision;
                TextBox box = window.RangeStartBox; // 하단 바 — 항상 보인다
                Assert.True(box.Focus());
                window.KeyTextInput("5");
                window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
                Assert.Equal(revision, editor.Revision);
                Assert.Equal(1.5f, editor.Document.Find("nod")!.Duration);
                Assert.True(box.IsFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 값_칸은_Enter_한_번이_되돌리기_한_번이고_포커스가_떠나도_같은_편집을_다시_기록하지_않는다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");
                var editor = window.Session.Editor;
                var y = new CurveRef("CharacterPortrait_Track_Move_Y", ChannelKind.Position, Axis.Y);
                editor.AddChannel("nod", y.Node, y.Kind);
                editor.SetKeys("nod", y.Node, y.Kind, y.Axis, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.3f, -18f), TestDocuments.Key(0.6f, 0f) });
                float ValueAt1() => y.Resolve(editor.Document.Find("nod")!)!.Keys[1].V;

                window.Graph.Select(new[] { new KeyRef(y, 1) }, additive: false);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                TextBox box = window.KeyValueBox;
                Assert.True(box.IsEnabled);

                // 값 입력 → Enter: 되돌리기 한 칸. Enter 뒤 포커스는 칸 밖이라 이어지는 Ctrl+Z는 문서 되돌리기다.
                long before = editor.Revision;
                box.Focus();
                box.Text = "7";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(7f, ValueAt1());
                Assert.Equal(before + 1, editor.Revision);
                Assert.False(box.IsFocused);

                // 다른 패널 클릭(포커스 이동)으로는 같은 편집이 다시 기록되지 않는다.
                box.Focus();
                window.Graph.Focus();
                Assert.Equal(before + 1, editor.Revision);

                window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
                Assert.Equal(-18f, ValueAt1());
                Assert.True(editor.CanRedo);

                // 되돌린 뒤 같은 값을 확정하거나 포커스만 옮겨도 다시하기가 남는다.
                window.Graph.Select(new[] { new KeyRef(y, 1) }, additive: false);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                box.Focus();
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                box.Focus();
                window.Graph.Focus();
                Assert.Equal(-18f, ValueAt1());
                Assert.True(editor.CanRedo);

                // 실제 새 편집은 다시하기를 지운다.
                box.Focus();
                box.Text = "+=2";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(-16f, ValueAt1());
                Assert.False(editor.CanRedo);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 복합_조작은_되돌리기_한_칸이다_여러_성분_pending_S()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                const string node = "CharacterPortrait_Track_Move";
                TestDocuments.OpenActor(window);
                window.AddTimelineFromForm("step", TestDocuments.Actor, "0.6");
                var editor = window.Session.Editor;
                TimelineAsset Step() => editor.Document.Find("step")!;
                window.AutoKeyChecked = false;
                window.SelectObject(node);
                window.Viewport.ScrubTo(0.25);

                // 좌우·상하 두 성분을 키 없는 포즈로 두고 S — 키 확정은 한 칸(가만히 선 값이 0이라 키 값 = 넣은 값).
                window.SetChannelBoxValue(ChannelKind.Position, Axis.X, "10");
                window.SetChannelBoxValue(ChannelKind.Position, Axis.Y, "-5");
                Assert.True(window.Viewport.HasPending);
                Assert.Null(Step().FindChannel(node, ChannelKind.Position));
                long before = editor.Revision;
                window.SetKeyFromShell();
                ChannelAsset body = Step().FindChannel(node, ChannelKind.Position)!;
                Assert.Equal(10f, body.X!.Keys.Single().V, 3);
                Assert.Equal(-5f, body.Y!.Keys.Single().V, 3);
                Assert.Equal(before + 1, editor.Revision);
                editor.Undo();
                Assert.Null(Step().FindChannel(node, ChannelKind.Position)); // 둘 다 한 번에
                Assert.True(window.Viewport.HasPending); // 키 확정 직전의 키 없는 포즈로
                editor.Redo();
                Assert.False(window.Viewport.HasPending);
                body = Step().FindChannel(node, ChannelKind.Position)!;
                Assert.Equal(10f, body.X!.Keys.Single().V, 3);
                Assert.Equal(-5f, body.Y!.Keys.Single().V, 3);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 세트를_고치면_세운_계층이_따라오고_키_편집은_자산을_다시_읽지_않는다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                var editor = window.Session.Editor;
                TestDocuments.OpenActor(window);

                // 키·메타 편집과 그 되돌리기는 자산을 다시 읽지 않는다.
                window.AddTimelineFromForm("nod", TestDocuments.Actor, "0.6");
                int refreshes = window.AssetRefreshCount;
                int syncs = window.Session.AssetSyncCount;
                editor.SetDuration("nod", 1f);
                editor.Undo();
                editor.Redo();
                Assert.Equal(refreshes, window.AssetRefreshCount);
                Assert.Equal(syncs, window.Session.AssetSyncCount);

                // 세트의 구조: 오브젝트를 더하면 세운 계층이 따라오고, 되돌리면 빠진다 — 한 번씩.
                long revision = editor.Revision;
                editor.AddObject(TestDocuments.Actor, "Prop", "", image: false);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(refreshes + 1, window.AssetRefreshCount);
                Assert.Equal(syncs + 1, window.Session.AssetSyncCount);
                Assert.True(window.Viewport.CurrentPose()!.Contains("set/Prop"));
                editor.Undo();
                Assert.Equal(refreshes + 2, window.AssetRefreshCount);
                Assert.Equal(syncs + 2, window.Session.AssetSyncCount);
                Assert.False(window.Viewport.CurrentPose()!.Contains("set/Prop"));
                editor.Redo();
                Assert.Equal(refreshes + 3, window.AssetRefreshCount);
                Assert.True(window.Viewport.CurrentPose()!.Contains("set/Prop"));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
