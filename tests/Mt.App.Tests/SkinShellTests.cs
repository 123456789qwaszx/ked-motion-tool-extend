using Avalonia;
using Mt.App.Views;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;

namespace Mt.App.Tests;

/// <summary>스킨과 조각의 화면(<c>PLAN-parts.md</c> P1) — 조각마다 그림을 주면 그 조각만 그려진다.</summary>
public class SkinShellTests
{
    private static MainWindow NewWindow()
    {
        string path = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"settings-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return new(path);
    }

    [Fact]
    public void 그림_오브젝트는_입혀_보는_스킨이_그림을_준_것만_그린다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                window.InspectorTab = 1; // 오브젝트 탭
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 아무것도 안 열었으면 스킨 칸이 없다.
                Assert.False(window.SkinFormState.Visible);
                Assert.Null(window.AddSkinFromForm());

                // 그림 오브젝트(조각)가 있는 세트를 열면 스킨 칸이 선다. 스킨이 없으면 조각은 하나도 안 그려진다.
                editor.AddRig(PartsRigFixture.Rig());
                window.SelectSet(PartsRigFixture.RigKind);
                Assert.Equal((true, (string?)null, 0), window.SkinFormState);
                Assert.Empty(window.Viewport.DrawnParts());

                // 스킨 추가 — 만들어서 입혀 본다, 한 칸. 조각 줄 넷이 선다.
                long revision = editor.Revision;
                Assert.Equal("skin", window.AddSkinFromForm());
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal("skin", window.Viewport.SetSkin);
                Assert.Equal((true, "skin", 4), window.SkinFormState);
                Assert.Empty(window.Viewport.DrawnParts()); // 그림을 준 조각이 아직 없다

                // 조각 둘에 그림(경로 칸에 넣고 Enter) — 그 둘만 그려진다. 파일이 없어도 자리표시로 그린다.
                TextBox part1 = window.SkinPartBox("Badge_Part1")!;
                part1.Focus();
                part1.Text = "art/flower-big.png";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                editor.SetSkinPart("skin", "Badge_Part3", "art/flower-small.png");
                Assert.Equal(new[] { "Badge_Part1", "Badge_Part3" }, window.Viewport.DrawnParts());
                Assert.Equal("art/flower-big.png", editor.Document.FindSkin("skin")!.Parts["Badge_Part1"]);
                Assert.Equal("art/flower-big.png", window.SkinPartBox("Badge_Part1")!.Text);

                // 조각은 노드의 rect(160×160)로, 무대 가운데에 선다.
                Quad quad = window.Viewport.PartQuad("Badge_Part1")!.Value;
                Assert.Equal(PartsRigFixture.PartSize, quad.BottomRight.X - quad.BottomLeft.X, 2);
                Assert.Equal(PartsRigFixture.PartSize, quad.TopLeft.Y - quad.BottomLeft.Y, 2);
                Assert.Equal(0f, quad.Center.X, 2);
                Assert.Null(window.Viewport.PartQuad("Nope"));

                // 조각마다 따로 움직인다 — 애니메이션은 그 세트의 보통 애니메이션이다.
                editor.AddMotion("float", PartsRigFixture.RigKind, 1f, MotionEnd.Commit, MotionChannelMode.Absolute);
                editor.AddChannel("float", "Badge_Part1", MotionChannelKind.Position);
                editor.SetKeys("float", "Badge_Part1", MotionChannelKind.Position, Axis.X, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(1f, 100f) });
                window.SelectMotion("float");
                window.Viewport.ScrubTo(1.0);
                Assert.Equal(100f, window.Viewport.PartQuad("Badge_Part1")!.Value.Center.X, 2);
                Assert.Equal(0f, window.Viewport.PartQuad("Badge_Part3")!.Value.Center.X, 2);
                Assert.Equal(0, window.ProblemRowsShown); // 그 세트의 오브젝트로 검증된다

                // 대표 사각형(손잡이가 서는 자리의 기준)은 첫 조각이다.
                Assert.Equal(100f, window.Viewport.ShownQuad()!.Value.Center.X, 2);

                // 스킨 둘 — 입혀 보는 것을 바꾸면 그 스킨의 그림만 그려진다(보기 상태 — 문서는 그대로).
                editor.AddSkin("plain", PartsRigFixture.RigKind);
                revision = editor.Revision;
                window.Viewport.SetSkin = "plain";
                Assert.Empty(window.Viewport.DrawnParts());
                window.Viewport.SetSkin = "skin";
                Assert.Equal(2, window.Viewport.DrawnParts().Count);
                Assert.Equal(revision, editor.Revision);

                // 스킨을 다 지우면 다시 안 그려진다.
                editor.RemoveSkin("skin");
                editor.RemoveSkin("plain");
                Assert.Null(window.Viewport.SetSkin);
                Assert.Empty(window.Viewport.DrawnParts());
                Assert.Equal((true, (string?)null, 0), window.SkinFormState);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 불투명도_칸에_값을_넣으면_알파_채널의_키가_되고_조각이_그만큼_흐려진다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                editor.AddRig(PartsRigFixture.Rig());
                window.SelectSet(PartsRigFixture.RigKind);
                window.AddSkinFromForm();
                editor.SetSkinPart("skin", "Badge_Part1", "art/sweat.png");
                editor.AddMotion("vanish", PartsRigFixture.RigKind, 1f, MotionEnd.Commit);
                window.SelectMotion("vanish");
                window.Viewport.SnapEnabled = false;
                window.InspectorTab = 2; // 모션 탭
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 불투명도를 받을 수 없는 노드(Anchor)를 고르면 칸이 없다. 조각을 고르면 선다.
                window.Viewport.SelectedNode = "Badge_Anchor";
                window.Viewport.ScrubTo(0.0);
                Assert.False(window.AlphaBox.Visible);
                window.Viewport.SelectedNode = "Badge_Part1";
                window.Viewport.ScrubTo(0.0001);
                window.Viewport.ScrubTo(0.0);
                Assert.True(window.AlphaBox.Visible);
                Assert.Equal("1", window.AlphaBox.Box.Text);

                // 처음에 1, 끝에 0 — 값을 넣고 Enter가 그 시각의 키(한 칸). 없던 알파 채널이 생긴다.
                window.Viewport.SetAlphaValue("Badge_Part1", 1f);
                window.Viewport.ScrubTo(1.0);
                long revision = editor.Revision;
                TextBox box = window.AlphaBox.Box;
                box.Focus();
                box.Text = "0";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(revision + 1, editor.Revision);
                ChannelAsset channel = Assert.Single(editor.Document.Find("vanish")!.Channels);
                Assert.Equal(("Badge_Part1", MotionChannelKind.Alpha), (channel.Node, channel.Kind));
                Assert.Equal(new[] { (0f, 1f), (1f, 0f) }, channel.X!.Keys.Select(k => (k.T, k.V)));
                Assert.Equal(0, window.ProblemRowsShown);

                // 뷰포트: 가운데에서 절반, 끝에서 0(안 그려진다). 다른 조각은 그대로 1.
                window.Viewport.ScrubTo(0.5);
                Assert.Equal(0.5f, window.Viewport.NodeOpacity("Badge_Part1")!.Value, 2);
                Assert.Equal(1f, window.Viewport.NodeOpacity("Badge_Part2")!.Value, 3);
                Assert.Equal("0.5", window.AlphaBox.Box.Text);
                window.Viewport.ScrubTo(1.0);
                Assert.Equal(0f, window.Viewport.NodeOpacity("Badge_Part1")!.Value, 3);

                // 묶음(Body)의 불투명도는 조각의 것과 곱해진다.
                window.Viewport.SelectedNode = "Badge_Body";
                window.Viewport.ScrubTo(0.0);
                window.Viewport.SetAlphaValue("Badge_Body", 0.5f);
                window.Viewport.ScrubTo(1.0);
                window.Viewport.SetAlphaValue("Badge_Body", 0.5f);
                window.Viewport.ScrubTo(0.5);
                Assert.Equal(0.25f, window.Viewport.NodeOpacity("Badge_Part1")!.Value, 2);
                Assert.Equal(0.5f, window.Viewport.NodeOpacity("Badge_Part2")!.Value, 2);

                // 범위 밖 값은 0~1로 죈다. 받을 수 없는 오브젝트(Anchor)에는 안 놓인다.
                window.Viewport.SetAlphaValue("Badge_Body", 7f);
                Assert.All(editor.Document.Find("vanish")!.FindChannel("Badge_Body", MotionChannelKind.Alpha)!.X!.Keys, k => Assert.InRange(k.V, 0f, 1f));
                revision = editor.Revision;
                window.Viewport.SetAlphaValue("Badge_Anchor", 0.3f);
                Assert.Equal(revision, editor.Revision);

                // 알파가 든 모션은 motions/3으로 나간다.
                string export = Path.Combine(Path.GetTempPath(), $"ked-motion-tool-{Guid.NewGuid():N}.json");

                try
                {
                    Assert.True(window.Session.Export(export), window.Session.Status);
                    string json = File.ReadAllText(export);
                    Assert.Contains("\"schema\": \"motions/3\"", json, StringComparison.Ordinal);
                    Assert.Contains("\"kind\": \"alpha\"", json, StringComparison.Ordinal);
                }
                finally
                {
                    File.Delete(export);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 그래프_목록의_키_없음_줄에서_지금_값을_키로_놓아_채널을_만든다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                editor.AddRig(PartsRigFixture.Rig());
                window.SelectSet(PartsRigFixture.RigKind);
                editor.AddMotion("bob", PartsRigFixture.RigKind, 0.4f);
                editor.AddChannel("bob", "Badge_Body", MotionChannelKind.Position);
                editor.SetKeys("bob", "Badge_Body", MotionChannelKind.Position, Axis.Y, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.2f, 10f), TestDocuments.Key(0.4f, 0f) });
                window.SelectMotion("bob");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // bob이 건드리는 것은 Body의 위치뿐이다. 나머지(다른 노드 · 배율 · 회전 · 불투명도)가 "키 없음"으로 흐리게 선다.
                Assert.DoesNotContain(("Badge_Body", MotionChannelKind.Position), window.UnkeyedRows);
                Assert.Contains(("Badge_Body", MotionChannelKind.Scale), window.UnkeyedRows);
                Assert.Contains(("Badge_Body", MotionChannelKind.Alpha), window.UnkeyedRows);
                Assert.Contains(("Badge_Part1", MotionChannelKind.Euler), window.UnkeyedRows);
                Assert.DoesNotContain(("Badge_Anchor", MotionChannelKind.Alpha), window.UnkeyedRows); // 불투명도를 못 받는 노드

                // ＋ — 지금 값이 지금 시각(0초)의 키가 되고 채널이 생긴다: 한 칸. 줄은 "키 없음"에서 빠지고 그 노드가 골라진다.
                long before = editor.Revision;
                window.KeyUnkeyedRow("Badge_Body", MotionChannelKind.Scale);
                Assert.Equal(before + 1, editor.Revision);
                ChannelAsset scale = editor.Document.Find("bob")!.FindChannel("Badge_Body", MotionChannelKind.Scale)!;
                Assert.Equal((0f, 1f, 1f), (scale.X!.Keys.Single().T, scale.X!.Keys.Single().V, scale.Y!.Keys.Single().V));
                Assert.DoesNotContain(("Badge_Body", MotionChannelKind.Scale), window.UnkeyedRows);
                Assert.Equal("Badge_Body", window.Viewport.SelectedNode);
                editor.Undo();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Contains(("Badge_Body", MotionChannelKind.Scale), window.UnkeyedRows);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 문서가_정의한_세트는_세션의_리그에_서고_저장하고_다시_열어도_선다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                Assert.Empty(window.Session.Rig.RigKinds); // 새 문서 — 아는 리그가 없다

                // 문서에 세트를 정의하면 세션의 리그에 선다(문서가 정의한 것만의 한 벌이다).
                editor.AddRig(PartsRigFixture.Rig());
                Assert.Equal(new[] { PartsRigFixture.RigKind }, window.Session.Rig.RigKinds);

                // 연다.
                window.SelectSet(PartsRigFixture.RigKind);
                Assert.True(window.Viewport.CurrentPose()!.Contains($"set/Badge_Part1"));       // 뷰포트에 섰다
                Assert.Equal(0, window.ProblemRowsShown);

                // 스킨 칸이 서고(조각 넷), 스킨을 입혀 조각을 그린다.
                window.InspectorTab = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(window.SkinFormState.Visible);
                window.AddSkinFromForm();
                Assert.Equal(4, window.SkinFormState.PartRows);
                editor.SetSkinPart("skin", "Badge_Part1", "art/sweat.png");
                Assert.Equal(new[] { "Badge_Part1" }, window.Viewport.DrawnParts());

                // 모션 — 이 리그의 노드에, 불투명도도.
                editor.AddMotion("pop", PartsRigFixture.RigKind, 0.2f, MotionEnd.Commit, MotionChannelMode.Absolute);
                editor.AddChannel("pop", "Badge_Body", MotionChannelKind.Position);
                editor.SetKeys("pop", "Badge_Body", MotionChannelKind.Position, Axis.Y, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.2f, 40f) });
                window.SelectMotion("pop");
                window.Viewport.SelectedNode = "Badge_Part1";
                Assert.True(window.Viewport.CanTakeAlpha("Badge_Part1"));
                Assert.Equal(0, window.ProblemRowsShown);

                // 리그의 노드를 지우면(편집기 명령) 뷰포트의 리그가 바로 따라온다.
                editor.RemoveRigNode(PartsRigFixture.RigKind, "Badge_Part3");
                Assert.False(window.Viewport.CurrentPose()!.Contains($"set/Badge_Part3"));
                editor.Undo();
                Assert.True(window.Viewport.CurrentPose()!.Contains($"set/Badge_Part3"));

                // 내보내기 둘: 리그(리그 스키마 모양) · 모션.
                string directory = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"rig-out-{Guid.NewGuid():N}");
                Directory.CreateDirectory(directory);
                Assert.True(window.Session.ExportRigs(Path.Combine(directory, "rigs.json")), window.Session.Status);
                Assert.True(window.Session.Export(Path.Combine(directory, "motions.json")), window.Session.Status);
                Assert.Contains("\"hasImage\": true", File.ReadAllText(Path.Combine(directory, "rigs.json")), StringComparison.Ordinal);
                Assert.Contains($"\"rig\": \"{PartsRigFixture.RigKind}\"", File.ReadAllText(Path.Combine(directory, "motions.json")), StringComparison.Ordinal);

                // 저장하고 다시 열어도 리그가 선다.
                string saved = Path.Combine(directory, "a.motion-authoring.json");
                window.Session.Save(saved);
                window.Session.New();
                Assert.Empty(window.Session.Rig.RigKinds);
                Assert.Null(window.Viewport.SetView);       // 새 문서 — 연 것이 없다
                Assert.Null(window.Viewport.CurrentPose());
                window.Session.Open(saved);
                Assert.Contains(PartsRigFixture.RigKind, window.Session.Rig.RigKinds);
                window.SelectSet(PartsRigFixture.RigKind);
                Assert.True(window.Viewport.CurrentPose()!.Contains($"set/Badge_Part1"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 세트를_열면_그_세트만_보이고_하이어아키에서_오브젝트를_짓고_고친다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 처음에는 세트가 없다 — 뷰포트는 빈 무대다.
                Assert.Empty(window.SetEditorState.Sets);
                Assert.Null(window.Viewport.SetView);
                Assert.Null(window.Viewport.CurrentPose());
                Assert.Null(window.AddSetNodeFromForm(image: true)); // 연 세트가 없으면 아무 일도 없다

                // 새 세트 — 한 칸. 목록에 서고, 열리고, 뷰포트와 하이어아키가 그 세트만 보인다.
                long revision = editor.Revision;
                Assert.Equal("set", window.NewSetFromForm());
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(new[] { "set" }, window.SetEditorState.Sets);
                Assert.Equal(("set", "Body", true), (window.SetEditorState.Set, window.SetEditorState.Node, window.SetEditorState.Editable));
                Assert.Equal("set", window.Viewport.SetView);
                Assert.True(window.Viewport.CurrentPose()!.Contains("set/Body"));
                Assert.Equal(new[] { "세트  set", "Body" }, window.HierarchyPathOf("Body"));

                // 그림 오브젝트 둘 — 고른 오브젝트 밑에 서고 뷰포트의 세트에 바로 선다.
                Assert.Equal("Part1", window.AddSetNodeFromForm(image: true));
                window.SelectSetNode("Body");
                Assert.Equal("Part2", window.AddSetNodeFromForm(image: true));
                Assert.Equal(new[] { ("Body", ""), ("Part1", "Body"), ("Part2", "Body") }, editor.Document.FindRig("set")!.Nodes.Select(n => (n.Id, n.Parent)));
                Assert.True(window.Viewport.CurrentPose()!.Contains("set/Part2"));

                // 하이어아키에서 오브젝트를 고르면 "오브젝트" 탭의 칸이 그것을 본다. 칸에서 자리를 고친다(Enter) — 한 칸, 뷰포트가 따라온다.
                window.InspectorTab = 1;
                window.SelectSetNode("Part2");
                Assert.Equal("Part2", window.SetEditorState.Node);
                Assert.Equal("Part2", window.RigNodeBoxes.Id.Text);
                revision = editor.Revision;
                window.RigNodeBoxes.X.Focus();
                window.RigNodeBoxes.X.Text = "40";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(40f, editor.Document.FindRig("set")!.Find("Part2")!.X);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(40f, window.Viewport.CurrentPose()!.GetState("set/Part2").AnchoredPosition.X, 3);

                // 이름 바꾸기 — 고른 것이 새 이름을 따라간다.
                window.RigNodeBoxes.Id.Focus();
                window.RigNodeBoxes.Id.Text = "Spark";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal("Spark", window.SetEditorState.Node);
                Assert.NotNull(editor.Document.FindRig("set")!.Find("Spark"));

                // 순서와 지우기.
                window.MoveSetNodeFromForm(-1);
                Assert.Equal(new[] { "Body", "Spark", "Part1" }, editor.Document.FindRig("set")!.Nodes.Select(n => n.Id));
                window.RemoveSetNodeFromForm();
                Assert.Equal(new[] { "Body", "Part1" }, editor.Document.FindRig("set")!.Nodes.Select(n => n.Id));
                Assert.Equal("Body", window.SetEditorState.Node);

                // 그 세트의 애니메이션(모션)을 고르면 세트가 연기한다 — 세트는 열린 채다.
                editor.AddMotion("in", "set", 0.5f, MotionEnd.Commit, MotionChannelMode.Absolute);
                editor.AddChannel("in", "Part1", MotionChannelKind.Position);
                editor.SetKeys("in", "Part1", MotionChannelKind.Position, Axis.X, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.5f, 100f) });
                window.SelectMotion("in");
                Assert.Equal("set", window.SelectedSet);
                window.Viewport.ScrubTo(0.5);
                Assert.Equal(100f, window.Viewport.CurrentPose()!.GetState("set/Part1").AnchoredPosition.X, 2);

                // 세트를 연 동안 목록에는 그 세트의 애니메이션만 있다 — 다른 것의 모션은 세트를 닫아야 고른다. 닫으면 빈 무대다.
                editor.AddMotion("nod", "elsewhere", 0.6f);
                Assert.DoesNotContain("nod", window.MotionListState.Names);
                window.SelectSet(null);
                Assert.Null(window.Viewport.SetView);
                Assert.Null(window.Viewport.CurrentPose());
                window.SelectMotion("nod");
                Assert.Equal("nod", window.SelectedMotion);

                // 다시 열었다가, 세트를 만든 것까지 되돌리면 목록에서 사라지고 빈 무대로 돌아간다.
                window.SelectSet("set");
                Assert.Equal("set", window.Viewport.SetView);
                Assert.Null(window.SelectedMotion);

                while (editor.Document.FindRig("set") is not null)
                {
                    editor.Undo();
                }

                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Empty(window.SetEditorState.Sets);
                Assert.Null(window.Viewport.SetView);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 스킨의_그림_넘기기와_놓임이_뷰포트에_그대로_보인다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                editor.AddRig(PartsRigFixture.Rig());
                window.SelectSet(PartsRigFixture.RigKind);
                window.AddSkinFromForm();
                window.InspectorTab = 1; // 오브젝트 탭
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 그림 칸에 ' | '로 이어 적으면 장이 여럿 — 한 칸.
                long revision = editor.Revision;
                TextBox images = window.SkinPartBox("Badge_Part1")!;
                images.Focus();
                images.Text = "art/a.png | art/b.png | art/c.png";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(new[] { "art/a.png", "art/b.png", "art/c.png" }, editor.Document.FindSkin("skin")!.ImagesOf("Badge_Part1"));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("art/a.png | art/b.png | art/c.png", window.SkinPartBox("Badge_Part1")!.Text);
                Assert.Null(window.SkinSetupBoxes("Badge_Part2")); // 그림이 없는 조각에는 덧붙임 칸이 없다

                // 시계를 따라 넘어간다(장당 0.2초).
                editor.AddMotion("still", PartsRigFixture.RigKind, 1f);
                window.SelectMotion("still");
                window.Viewport.ScrubTo(0.0);
                Assert.Equal("art/a.png", window.Viewport.PartImage("Badge_Part1"));
                window.Viewport.ScrubTo(0.25);
                Assert.Equal("art/b.png", window.Viewport.PartImage("Badge_Part1"));
                window.Viewport.ScrubTo(0.5);
                Assert.Equal("art/c.png", window.Viewport.PartImage("Badge_Part1"));

                // 놓임 칸(X) — 한 칸, 조각이 그만큼 옮겨 그려진다. 노드의 값은 그대로다(모션과 따로).
                Quad before = window.Viewport.PartQuad("Badge_Part1")!.Value;
                var boxes = window.SkinSetupBoxes("Badge_Part1")!;
                revision = editor.Revision;
                boxes.X.Focus();
                boxes.X.Text = "50";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(50f, editor.Document.FindSkin("skin")!.SetupOf("Badge_Part1").X);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Quad placed = window.Viewport.PartQuad("Badge_Part1")!.Value;
                // 놓임은 조각 노드의 px이다 — 화면에서는 부모들의 배율이 곱해진다: 조각의 폭이 곱해진 그만큼.
                float perPixel = (before.BottomRight.X - before.BottomLeft.X) / PartsRigFixture.PartSize;
                Assert.Equal(before.Center.X + 50f * perPixel, placed.Center.X, 2);
                Assert.Equal(0f, window.Viewport.CurrentPose()!.GetState($"set/Badge_Part1").AnchoredPosition.X, 3);

                // 크기 0.5 — 사각형이 반으로.
                SkinPartSetup half = editor.Document.FindSkin("skin")!.SetupOf("Badge_Part1").Clone();
                half.Scale = 0.5f;
                editor.SetSkinPartSetup("skin", "Badge_Part1", half);
                Quad small = window.Viewport.PartQuad("Badge_Part1")!.Value;
                Assert.Equal((before.BottomRight.X - before.BottomLeft.X) / 2f, small.BottomRight.X - small.BottomLeft.X, 2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 배치_모드에서_그림을_가져와_놓고_끌어서_가만히_선_자세를_잡는다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;

                // 그림 견본 하나를 alert.png라는 이름으로 — 오브젝트의 이름은 파일에서 온다.
                string art = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"art-{Guid.NewGuid():N}", "alert.png");
                Directory.CreateDirectory(Path.GetDirectoryName(art)!);
                File.Copy(TestDocuments.ArtPath("10_microbe_cell.png"), art);
                window.NewSetFromForm();
                window.InspectorTab = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(view.IsLayoutMode);
                RigAsset Rig() => editor.Document.FindRig("set")!;

                // 그림 가져오기 — 오브젝트(이름은 파일에서, 크기는 그림의 것) + 자리 + 스킨의 그림이 한 칸. 스킨이 없어 세트의 이름으로 만들어 입혀 본다.
                long revision = editor.Revision;
                Assert.Equal("alert", window.AddImageObjectFromForm(art, new Vec2(100f, 50f)));
                Assert.Equal(revision + 1, editor.Revision);
                RigNodeAsset node = Rig().Find("alert")!;
                Assert.Equal(("Body", 100f, 50f, 239f, 114f, true), (node.Parent, node.X, node.Y, node.Width, node.Height, node.Image));
                Assert.Equal("set", view.SetSkin);
                Assert.EndsWith("alert.png", editor.Document.FindSkin("set")!.Parts["alert"], StringComparison.Ordinal);
                Assert.Equal("alert", window.SetEditorState.Node);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(new[] { "alert" }, view.DrawnParts());
                Assert.Equal("alert_2", window.AddImageObjectFromForm(art, new Vec2(-200f, 0f))); // 같은 이름이면 번호가 붙는다
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Point At(Vec2 p) => view.TranslatePoint(view.ToScreen(p), window) ?? throw new InvalidOperationException("뷰가 창에 붙어 있지 않다");
                Vec2 Center(string id) => view.PartQuad(id)!.Value.Center;

                // 끌기 = 자리. 집은 것이 하이어아키·칸에서도 골라진다. 끈 하나가 한 칸이고 끄는 동안에는 기록이 없다. 모션은 안 생긴다.
                revision = editor.Revision;
                Vec2 grab = Center("alert");
                var drop = new Vec2(grab.X + 60f, grab.Y - 40f);
                window.MouseDown(At(grab), MouseButton.Left);
                Assert.Equal("alert", window.SetEditorState.Node);
                window.MouseMove(At(new Vec2(grab.X + 20f, grab.Y)), RawInputModifiers.LeftMouseButton);
                Assert.Equal(revision, editor.Revision);
                Assert.True(Center("alert").X > grab.X + 10f);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal((160f, 10f), (Rig().Find("alert")!.X, Rig().Find("alert")!.Y));
                Assert.Empty(editor.Document.Motions);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("160", window.RigNodeBoxes.X.Text);

                // Shift+끌기 = 배율(원점에서의 거리 비), Ctrl+끌기 = 회전.
                Vec2 c = Center("alert");
                window.MouseDown(At(new Vec2(c.X + 50f, c.Y)), MouseButton.Left, RawInputModifiers.Shift);
                window.MouseMove(At(new Vec2(c.X + 25f, c.Y)), RawInputModifiers.LeftMouseButton | RawInputModifiers.Shift);
                window.MouseUp(At(new Vec2(c.X + 25f, c.Y)), MouseButton.Left, RawInputModifiers.Shift);
                Assert.Equal((0.5f, 0.5f), (Rig().Find("alert")!.ScaleX, Rig().Find("alert")!.ScaleY));
                c = Center("alert");
                window.MouseDown(At(new Vec2(c.X + 20f, c.Y)), MouseButton.Left, RawInputModifiers.Control);
                window.MouseMove(At(new Vec2(c.X, c.Y + 20f)), RawInputModifiers.LeftMouseButton | RawInputModifiers.Control);
                window.MouseUp(At(new Vec2(c.X, c.Y + 20f)), MouseButton.Left, RawInputModifiers.Control);
                Assert.Equal(90f, Rig().Find("alert")!.Rotation, 0);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(("90", "0.5"), (window.RigNodePoseBoxes.Rotation.Text, window.RigNodePoseBoxes.ScaleX.Text));

                // 칸에서도 고친다.
                window.RigNodePoseBoxes.Rotation.Focus();
                window.RigNodePoseBoxes.Rotation.Text = "15";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(15f, Rig().Find("alert")!.Rotation);

                // 제자리에서 눌렀다 떼면 고르기만 한다(기록 없음).
                revision = editor.Revision;
                Vec2 other = Center("alert_2");
                window.MouseDown(At(other), MouseButton.Left);
                window.MouseUp(At(other), MouseButton.Left);
                Assert.Equal(revision, editor.Revision);
                Assert.Equal("alert_2", window.SetEditorState.Node);

                // 그 세트의 애니메이션을 고르면 배치 모드가 아니다 — 끌기는 키가 된다(전부터 있던 길).
                editor.AddMotion("in", "set", 0.5f);
                window.SelectMotion("in");
                Assert.False(view.IsLayoutMode);
                Assert.Equal("set", window.SelectedSet);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 세트를_열면_그_세트의_애니메이션만_보이고_오브젝트를_따로_본다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                editor.AddMotion("nod", "elsewhere", 0.6f);
                window.NewSetFromForm();
                string a = window.AddSetNodeFromForm(image: true)!;   // Part1 — Body 밑
                window.SelectSetNode("Body");
                string b = window.AddSetNodeFromForm(image: true)!;   // Part2 — Body 밑
                window.SelectSetNode(b);
                string c = window.AddSetNodeFromForm(image: true)!;   // Part3 — Part2 밑
                editor.AddSkin("skin", "set");
                foreach (string part in new[] { a, b, c })
                {
                    editor.SetSkinPart("skin", part, $"art/{part}.png");
                }

                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                IReadOnlyList<string> Drawn() => view.DrawnParts();
                Assert.Equal(new[] { a, b, c }, Drawn());

                // 목록은 이 세트의 애니메이션만 — 다른 것의 nod는 안 보인다. "모션 추가"는 이 세트의 것을 만든다.
                Assert.Equal(("애니메이션 — 세트 set", 0), (window.MotionListState.Header, window.MotionListState.Names.Count));
                window.AddMotionFromButton();
                Assert.Equal("set", editor.Document.Find("motion")!.Rig);
                Assert.Equal(new[] { "motion" }, window.MotionListState.Names);
                Assert.Equal("motion", window.SelectedMotion);
                Assert.Equal("set", window.SelectedSet);  // 세트는 열린 채다
                Assert.False(view.IsLayoutMode);          // 애니메이션 모드

                // 이것만 보기 — 그 오브젝트와 그 밑의 것만 그려진다. 문서는 안 바뀐다.
                long revision = editor.Revision;
                window.SetSetNodeViewFromMenu(b, solo: true, hidden: null);
                Assert.Equal(new[] { b, c }, Drawn());
                Assert.False(view.IsSetNodeShown(a));

                // 솔로 안에서 숨기기 — 밑의 것만 숨긴다.
                window.SetSetNodeViewFromMenu(c, solo: null, hidden: true);
                Assert.Equal(new[] { b }, Drawn());

                // 솔로를 풀면 숨긴 것만 빠진다. 부모를 숨기면 밑의 것도 안 보인다.
                window.SetSetNodeViewFromMenu(b, solo: false, hidden: null);
                Assert.Equal(new[] { a, b }, Drawn());
                window.SetSetNodeViewFromMenu("Body", solo: null, hidden: true);
                Assert.Empty(Drawn());

                // 움직임 밀기 · 차례로(시차) — 고른 애니메이션에서 오브젝트의 키를 통째로. 각각 한 칸.
                editor.AddChannel("motion", a, MotionChannelKind.Position);
                editor.SetKeys("motion", a, MotionChannelKind.Position, Axis.X, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.2f, 0f), TestDocuments.Key(0.6f, 50f), TestDocuments.Key(1f, 50f) });
                editor.AddChannel("motion", b, MotionChannelKind.Position);
                editor.SetKeys("motion", b, MotionChannelKind.Position, Axis.X, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.2f, 0f), TestDocuments.Key(0.6f, 50f), TestDocuments.Key(1f, 50f) });
                float SecondKey(string node) => editor.Document.Find("motion")!.FindChannel(node, MotionChannelKind.Position)!.X!.Keys[1].T;
                long before = editor.Revision;
                window.ShiftSetNodeKeysFromMenu(a, 0.1f);
                Assert.Equal(before + 1, editor.Revision);
                Assert.Equal(0.3f, SecondKey(a), 3);
                editor.Undo();
                before = editor.Revision;
                window.StaggerSetNodesFromMenu(a, 0.1f); // a는 그대로, 그 뒤의 형제 b가 0.1초 늦게
                Assert.Equal(before + 1, editor.Revision);
                Assert.Equal((0.2f, 0.3f), (MathF.Round(SecondKey(a), 3), MathF.Round(SecondKey(b), 3)));

                // 무대만 보기 — 보기 상태(문서는 그대로).
                Assert.False(view.StageOnly);
                window.StageOnlyChecked = true;
                Assert.True(view.StageOnly);
                window.StageOnlyChecked = false;

                // 전부 보이기.
                window.SetSetNodeViewFromMenu(null, null, null);
                Assert.Equal(new[] { a, b, c }, Drawn());

                // 세트를 닫으면 따로 보기가 풀리고 목록이 전부로 돌아온다.
                window.SetSetNodeViewFromMenu(a, solo: null, hidden: true);
                window.SelectSet(null);
                Assert.Equal("모션", window.MotionListState.Header);
                Assert.Equal(new[] { "nod", "motion" }, window.MotionListState.Names);
                window.SelectSet("set");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(new[] { a, b, c }, Drawn());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 색_사각형을_더하면_스킨_없이_세트에_서고_끌어서_옮기고_칸에서_색을_바꾼다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                window.NewSetFromForm();
                window.InspectorTab = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                RigNodeAsset Node(string id) => editor.Document.FindRig("set")!.Find(id)!;

                // 색 사각형 더하기 — 오브젝트 + 크기 + 색이 한 칸. 스킨이 없어도 선다(세트의 오브젝트 사각형이 그 색 사각형이다).
                long revision = editor.Revision;
                Assert.Equal("Color1", window.AddColorNodeFromForm());
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(("Body", 400f, 300f, "#3D5A80", false), (Node("Color1").Parent, Node("Color1").Width, Node("Color1").Height, Node("Color1").Color, Node("Color1").Image));
                Assert.Null(view.SetSkin);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Quad quad = view.ShownQuad()!.Value;
                Assert.Equal(400f, quad.BottomRight.X - quad.BottomLeft.X, 1);
                Assert.Equal("#3D5A80", window.RigNodeColorBox.Text);

                // 칸에서 색을 바꾼다(Enter) — 한 칸. 틀린 글은 막히고 문서는 그대로다.
                window.RigNodeColorBox.Focus();
                window.RigNodeColorBox.Text = "ee6c4d";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal("#EE6C4D", Node("Color1").Color);
                window.RigNodeColorBox.Text = "red";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal("#EE6C4D", Node("Color1").Color);

                // 배치 모드에서 끌어 옮긴다 — 색은 그대로 남는다.
                Point At(Vec2 p) => view.TranslatePoint(view.ToScreen(p), window) ?? throw new InvalidOperationException("뷰가 창에 붙어 있지 않다");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Vec2 grab = view.ShownQuad()!.Value.Center;
                var drop = new Vec2(grab.X + 50f, grab.Y + 20f);
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal((50f, 20f, "#EE6C4D"), (Node("Color1").X, Node("Color1").Y, Node("Color1").Color));

                // 색 견본 — 이 문서에서 쓴 색이 먼저 서고, 고르면 바로 들어간다(한 칸).
                window.SelectSetNode("Color1");
                Assert.Equal("#EE6C4D", window.ColorSwatches()[0]);
                Assert.Contains("#243B6B", window.ColorSwatches());
                revision = editor.Revision;
                window.PickColorSwatch("#243B6B");
                Assert.Equal("#243B6B", Node("Color1").Color);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal("#243B6B", window.RigNodeColorBox.Text);

                // 색을 비우면 보통 오브젝트로 돌아간다.
                window.SelectSetNode("Color1");
                window.RigNodeColorBox.Focus();
                window.RigNodeColorBox.Text = "";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Null(Node("Color1").Color);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 층_표를_가져오면_그림_한_묶음이_세트의_오브젝트와_스킨으로_한_칸에_선다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                string sheet = TestDocuments.ArtPath("layers.tsv");
                Assert.Empty(window.ImportLayerSheetFromForm(sheet)); // 연 세트가 없으면 아무 일도 없다
                window.NewSetFromForm();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 한 칸에: 바탕색 + 층 넉 장, 스킨까지. 뷰포트의 세트에 바로 선다.
                long revision = editor.Revision;
                IReadOnlyList<string> created = window.ImportLayerSheetFromForm(sheet);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(new[] { "sign", "microbe_sun", "microbe_leaf", "microbe_cell" }, created);
                RigAsset rig = editor.Document.FindRig("set")!;
                Assert.Equal("#10262B", rig.Find("Backdrop")!.Color);
                Assert.Equal((889f, 249f, true), (rig.Find("sign")!.Width, rig.Find("sign")!.Height, rig.Find("sign")!.Image));
                Assert.Equal("set", window.Viewport.SetSkin);
                Assert.EndsWith("art/06_sign.png", editor.Document.FindSkin("set")!.Parts["sign"], StringComparison.Ordinal);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(created, window.Viewport.DrawnParts());

                // 되돌리면 통째로 사라진다(처음의 Body만 남는다).
                editor.Undo();
                Assert.Equal(new[] { "Body" }, editor.Document.FindRig("set")!.Nodes.Select(n => n.Id));
                Assert.Null(editor.Document.FindSkin("set"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 세트에_다른_세트를_넣으면_뷰포트에_펼쳐_서고_통째로_끌리고_원본을_고치면_따라온다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;

                // 세트 둘: car(색 사각형 하나) · city(빈 세트).
                Assert.Equal("set", window.NewSetFromForm());
                window.SelectSetNode("Body");
                window.AddColorNodeFromForm(); // set/Color1 — 400×300
                Assert.Equal("set_2", window.NewSetFromForm());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 세트 넣기 — 한 칸. 인스턴스가 골라지고, 뷰포트의 세트에 안쪽의 것이 경로 이름으로 선다.
                long revision = editor.Revision;
                window.SelectSetNode(null);
                Assert.Equal("set", window.AddSetInstanceFromMenu("set"));
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal("set", editor.Document.FindRig("set_2")!.Find("set")!.Set);
                Assert.Equal("set", window.SetEditorState.Node);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(view.CurrentPose()!.Contains("set/set.Color1"));
                Assert.True(view.IsLayoutMode);

                // 안쪽의 것을 눌러도 그것을 품은 오브젝트가 집힌다 — 끌면 통째로 옮겨진다(안쪽은 그대로).
                Point At(Vec2 p) => view.TranslatePoint(view.ToScreen(p), window) ?? throw new InvalidOperationException("뷰가 창에 붙어 있지 않다");
                Vec2 Inner() => view.CurrentPose()!.TransformPoint("set/set.Color1", Vec3.Zero).XY;
                Vec2 grab = Inner();
                var drop = new Vec2(grab.X + 80f, grab.Y + 40f);
                window.MouseDown(At(grab), MouseButton.Left);
                Assert.Equal("set", window.SetEditorState.Node);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                RigNodeAsset instance = editor.Document.FindRig("set_2")!.Find("set")!;
                Assert.Equal((80f, 40f, "set"), (instance.X, instance.Y, instance.Set));
                Assert.Equal(0f, editor.Document.FindRig("set")!.Find("Color1")!.X); // 원본 세트는 안 바뀐다
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(grab.X + 80f, Inner().X, 1);

                // 원본을 고치면 품은 곳이 따라온다(참조).
                editor.UpdateRigNode("set", "Color1", new RigNodeAsset { X = 25f, Width = 400f, Height = 300f, Color = "#3D5A80", Alpha = true });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(grab.X + 80f + 25f, Inner().X, 1);

                // 서로 품는 것은 막힌다 — 문서는 그대로다(메뉴에서는 상태줄에 까닭이 뜬다).
                window.SelectSet("set");
                revision = editor.Revision;
                Assert.Throws<InvalidOperationException>(() => window.AddSetInstanceFromMenu("set_2"));
                Assert.Equal(revision, editor.Revision);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 재생_블록_칸에서_품은_세트의_애니메이션을_지금_시각에_놓으면_뷰포트가_그것을_튼다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;

                // 세트 둘: set(색 사각형 + 애니메이션 slide: 0.4초 유지, x 0 → 100) · set_2(set을 품는다 + 애니메이션 city_in: 2초).
                window.NewSetFromForm();
                window.SelectSetNode("Body");
                window.AddColorNodeFromForm();
                editor.AddMotion("slide", "set", 0.4f, MotionEnd.Commit);
                editor.AddChannel("slide", "Color1", MotionChannelKind.Position);
                editor.SetKeys("slide", "Color1", MotionChannelKind.Position, Axis.X, new[]
                {
                    new KeyAsset { T = 0f, V = 0f, Tangent = TangentMode.Linear },
                    new KeyAsset { T = 0.4f, V = 100f, Tangent = TangentMode.Linear },
                });
                window.NewSetFromForm();
                window.SelectSetNode(null);
                window.AddSetInstanceFromMenu("set");
                editor.AddMotion("city_in", "set_2", 2f, MotionEnd.Commit);
                window.InspectorTab = 2; // 모션 탭
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 품은 세트가 있는 세트의 애니메이션을 고르면 재생 블록 칸이 보인다.
                window.SelectMotion("city_in");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(window.PlayPanelShown);
                Assert.Equal(0, window.PlayRowCount);

                // "재생 블록 추가 (지금 시각)" — 한 칸. 지금 시각에 들어가는 첫 (인스턴스, 애니메이션)이 놓인다.
                window.Graph.CurrentTime = 0.5;
                long revision = editor.Revision;
                Assert.Equal(0, window.AddMotionPlayFromForm());
                Assert.Equal(revision + 1, editor.Revision);
                MotionPlayAsset play = Assert.Single(editor.Document.Find("city_in")!.Plays);
                Assert.Equal(("set", "slide", 0.5f, 1), (play.Node, play.Motion, play.At, play.Repeat));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, window.PlayRowCount);
                Assert.Equal(("0.5", "set", "slide", "1"), (window.PlayRow(0).Time.Text, window.PlayRow(0).Node.SelectedItem, window.PlayRow(0).Motion.SelectedItem, window.PlayRow(0).Repeat.Text));

                // 타임 슬라이더가 블록의 구간을 막대로 그린다(0.5 ~ 0.9초).
                (double start, double end, string label) = Assert.Single(view.PlaySpans());
                Assert.Equal((0.5, 0.9, "set ▸ slide"), (Math.Round(start, 3), Math.Round(end, 3), label));

                // 막대를 끌면 블록의 시작 시각이 옮겨진다 — 놓을 때 한 칸. 막대 너비(0.4초)만큼 오른쪽으로.
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Rect bar = window.TimeSlider.PlayBarRects()[0];
                Point Slider(Point p) => window.TimeSlider.TranslatePoint(p, window) ?? throw new InvalidOperationException("슬라이더가 창에 붙어 있지 않다");
                revision = editor.Revision;
                window.MouseDown(Slider(bar.Center), MouseButton.Left);
                window.MouseMove(Slider(new Point(bar.Center.X + bar.Width, bar.Center.Y)), RawInputModifiers.LeftMouseButton);
                window.MouseUp(Slider(new Point(bar.Center.X + bar.Width, bar.Center.Y)), MouseButton.Left);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.InRange(editor.Document.Find("city_in")!.Plays[0].At, 0.8f, 1.0f);

                // 길이 밖으로 끌면 끝에 맞춰 선다(2 − 0.4 = 1.6초).
                bar = window.TimeSlider.PlayBarRects()[0];
                window.MouseDown(Slider(bar.Center), MouseButton.Left);
                window.MouseMove(Slider(new Point(bar.Center.X + 4000, bar.Center.Y)), RawInputModifiers.LeftMouseButton);
                window.MouseUp(Slider(new Point(bar.Center.X + 4000, bar.Center.Y)), MouseButton.Left);
                Assert.Equal(1.6f, editor.Document.Find("city_in")!.Plays[0].At, 2);
                editor.UpdateMotionPlay("city_in", 0, "set", "slide", 0.5f, 1);

                // 뷰포트가 품은 애니메이션을 튼다 — 블록 전에는 제자리, 가운데서 반, 끝난 뒤에는 남는다(유지).
                float X(double t)
                {
                    window.Graph.CurrentTime = t;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    return view.CurrentPose()!.TransformPoint("set/set.Color1", Vec3.Zero).X;
                }

                float rest = X(0.2);
                Assert.Equal(50f, X(0.7) - rest, 0);
                Assert.Equal(100f, X(1.5) - rest, 0);

                // 반복 칸을 2로(Enter) — 한 칸. 둘째 바퀴가 쌓인다.
                TextBox repeat = window.PlayRow(0).Repeat;
                repeat.Focus();
                repeat.Text = "2";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(2, editor.Document.Find("city_in")!.Plays[0].Repeat);
                Assert.Equal(200f, X(1.5) - rest, 0);

                // 길이를 넘는 시각은 막히고 문서는 그대로다.
                revision = editor.Revision;
                TextBox time = window.PlayRow(0).Time;
                time.Focus();
                time.Text = "1.9";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(revision, editor.Revision);

                // 줄의 "떼기" — 블록이 사라지고 그 움직임이 제 키로 굳는다(한 칸). 뷰포트의 재생은 그대로다.
                window.Viewport.Focus();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                revision = editor.Revision;
                window.DetachPlayRow(0);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Empty(editor.Document.Find("city_in")!.Plays);
                Assert.NotNull(editor.Document.Find("city_in")!.FindChannel("set.Color1", MotionChannelKind.Position));
                Assert.Equal(200f, X(1.5) - rest, 0);
                Assert.Equal(0, window.PlayRowCount);

                // 품은 세트가 없는 세트의 애니메이션에서는 칸이 숨는다.
                window.SelectSet("set");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.SelectMotion("slide");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(window.PlayPanelShown);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 하이어아키에서_새_세트로_빼면_인스턴스가_그_자리에_서고_세트_이름_칸에서_이름을_바꾼다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;
                window.NewSetFromForm();
                window.SelectSetNode(null);
                window.AddColorNodeFromForm(); // Color1 — 맨 위
                window.SelectSetNode(null);
                window.AddColorNodeFromForm(); // Color2
                editor.UpdateRigNode("set", "Color2", new RigNodeAsset { X = 200f, Y = 100f, Width = 400f, Height = 300f, Color = "#3D5A80", Alpha = true });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Vec2 before = view.CurrentPose()!.TransformPoint("set/Color2", Vec3.Zero).XY;

                // Color1부터 Color2까지를 새 세트로 — 한 칸. 이름은 첫 오브젝트에서, 그 자리에 인스턴스가 서고 골라진다.
                long revision = editor.Revision;
                Assert.Equal("color1", window.ExtractSetNodesFromMenu("Color1", "Color2"));
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal("color1", editor.Document.FindRig("set")!.Find("color1")!.Set);
                Assert.Equal(new[] { "Color1", "Color2" }, editor.Document.FindRig("color1")!.Nodes.Select(n => n.Id));
                Assert.Equal("color1", window.SetEditorState.Node);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 보이는 자리는 그대로다.
                Vec2 after = view.CurrentPose()!.TransformPoint("set/color1.Color2", Vec3.Zero).XY;
                Assert.Equal(before.X, after.X, 1);
                Assert.Equal(before.Y, after.Y, 1);

                // 세트 이름 칸 — 새 세트를 열어 이름을 고친다(Enter). 품은 곳이 따라오고 그 세트가 열린 채다.
                window.SelectSet("color1");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("color1", window.SetNameBox.Text);
                window.SetNameBox.Focus();
                window.SetNameBox.Text = "panels";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal("panels", editor.Document.FindRig("set")!.Find("panels")!.Set); // 세트의 이름을 딴 인스턴스도 따라온다
                Assert.Equal("panels", window.SelectedSet);

                // 있는 이름은 막히고 칸이 돌아온다.
                window.SetNameBox.Focus();
                window.SetNameBox.Text = "set";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.NotNull(editor.Document.FindRig("panels"));
                Assert.Equal("panels", window.SetNameBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 인스턴스를_고르면_품은_세트의_스킨을_고르고_안쪽_세트로_들어갔다_돌아온다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                window.NewSetFromForm();                 // set — 품길 세트(스킨 둘)
                editor.AddSkin("red", "set");
                editor.AddSkin("blue", "set");
                window.NewSetFromForm();                 // set_2 — 품는 세트
                window.SelectSetNode(null);
                window.AddSetInstanceFromMenu("set");
                window.InspectorTab = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 인스턴스를 고르면 스킨 칸이 선다 — 고르면 한 칸.
                Assert.True(window.RigInstanceFieldsShown);
                Assert.Equal(0, window.RigNodeSkinCombo.SelectedIndex);
                long revision = editor.Revision;
                window.RigNodeSkinCombo.SelectedItem = "blue";
                Assert.Equal("blue", editor.Document.FindRig("set_2")!.Find("set")!.SetSkin);
                Assert.Equal(revision + 1, editor.Revision);
                window.RigNodeSkinCombo.SelectedIndex = 0;
                Assert.Null(editor.Document.FindRig("set_2")!.Find("set")!.SetSkin);

                // 보통 오브젝트에서는 칸이 숨는다.
                window.SelectSetNode("Body");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(window.RigInstanceFieldsShown);

                // 안쪽 세트 열기 → 돌아가기 — 문서는 안 바뀌고, 돌아오면 그 인스턴스가 골라져 있다.
                window.SelectSetNode("set");
                revision = editor.Revision;
                Assert.False(window.SetBackShown);
                window.EnterSetFromForm();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("set", window.SelectedSet);
                Assert.True(window.SetBackShown);
                window.BackToOuterSetFromForm();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(("set_2", "set"), (window.SelectedSet, window.SetEditorState.Node));
                Assert.False(window.SetBackShown);
                Assert.Equal(revision, editor.Revision);

                // 들어간 뒤 목록에서 직접 다른 세트를 고르면 돌아갈 길이 사라진다.
                window.EnterSetFromForm();
                window.SelectSet("set_2");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(window.SetBackShown);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 다른_문서의_세트를_들이면_애니메이션과_스킨이_함께_오고_그림을_찾는다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;

                // 들일 문서 — 세트 둘: pillar가 sun을 품고, pillar_in이 sun_rise를 재생 블록으로 튼다. 그림은 그 문서 기준의 상대 경로다.
                string folder = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"import-{Guid.NewGuid():N}");
                Directory.CreateDirectory(Path.Combine(folder, "art"));
                File.Copy(TestDocuments.ArtPath("08_microbe_sun.png"), Path.Combine(folder, "art", "sun.png"));
                var source = new MotionEditor(new MotionDocument());
                source.AddRig(new RigAsset { Kind = "sun", Nodes = { new RigNodeAsset { Id = "Disc", Width = 242f, Height = 236f, Image = true, Alpha = true } } });
                source.AddSkin("sun", "sun");
                source.SetSkinPart("sun", "Disc", "art/sun.png");
                source.AddMotion("sun_rise", "sun", 0.5f, MotionEnd.Commit);
                source.AddChannel("sun_rise", "Disc", MotionChannelKind.Position);
                source.SetKeys("sun_rise", "Disc", MotionChannelKind.Position, Axis.Y, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.5f, 120f) });
                source.AddRig(new RigAsset { Kind = "pillar", Nodes = { new RigNodeAsset { Id = "Panel", Width = 400f, Height = 900f, Color = "#3D5A80", Alpha = true } } });
                source.AddRigInstance("pillar", "sun", "sun");
                source.AddMotion("pillar_in", "pillar", 1f, MotionEnd.Commit);
                source.AddChannel("pillar_in", "Panel", MotionChannelKind.Position);
                source.SetKeys("pillar_in", "Panel", MotionChannelKind.Position, Axis.X, new[] { TestDocuments.Key(0f, -600f), TestDocuments.Key(1f, 0f) });
                source.AddMotionPlay("pillar_in", "sun", "sun_rise", 0.25f);
                string path = Path.Combine(folder, "pillar.motion-authoring.json");
                File.WriteAllText(path, Mt.Authoring.Serialization.MotionDocumentJson.Write(source.Document));

                // 그 문서의 세트 둘(pillar · 그것이 품은 sun)이 애니메이션 · 스킨과 함께 — 한 칸.
                long revision = editor.Revision;
                Assert.Equal(new[] { "pillar", "sun" }, window.ImportSetsFromFile(path).OrderBy(k => k, StringComparer.Ordinal));
                Assert.Equal(revision + 1, editor.Revision);
                Assert.NotNull(editor.Document.Find("pillar_in"));
                Assert.NotNull(editor.Document.Find("sun_rise"));
                Assert.NotNull(editor.Document.FindSkin("sun"));
                Assert.NotNull(editor.Document.Find("pillar_in")!.Flattened); // 재생 블록도 따라와 구워진다

                // 그림은 그 문서의 자리를 가리킨다 — 전부 찾는다.
                Assert.NotEmpty(window.Session.SkinImages());
                Assert.All(window.Session.SkinImages(), image => Assert.True(image.Path is not null && File.Exists(image.Path), image.Image));

                // 세트 목록에 서고 열린다.
                window.SelectSet("sun");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("sun", window.SelectedSet);

                // 같은 이름이 이미 있으면 막힌다 — 문서는 그대로다.
                revision = editor.Revision;
                Assert.Throws<InvalidOperationException>(() => window.ImportSetsFromFile(path));
                Assert.Equal(revision, editor.Revision);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 품은_세트를_더블클릭하면_바깥을_보는_채로_안쪽의_오브젝트를_끌어_그_세트를_고친다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;

                // set(색 사각형 Color1) · set_2(set을 (300, 100)에 품는다).
                window.NewSetFromForm();
                window.SelectSetNode("Body");
                window.AddColorNodeFromForm();
                window.NewSetFromForm();
                window.SelectSetNode(null);
                window.AddSetInstanceFromMenu("set");
                editor.UpdateRigNode("set_2", "set", new RigNodeAsset { X = 300f, Y = 100f, Alpha = true });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Point At(Vec2 p) => view.TranslatePoint(view.ToScreen(p), window) ?? throw new InvalidOperationException("뷰가 창에 붙어 있지 않다");
                Vec2 Inner() => view.CurrentPose()!.TransformPoint("set/set.Color1", Vec3.Zero).XY;
                RigNodeAsset Color() => editor.Document.FindRig("set")!.Find("Color1")!;
                RigNodeAsset Instance() => editor.Document.FindRig("set_2")!.Find("set")!;

                // 더블클릭 — 안쪽으로 들어간다. 문서는 안 바뀐다.
                long revision = editor.Revision;
                Vec2 grab = Inner();
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseUp(At(grab), MouseButton.Left);
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseUp(At(grab), MouseButton.Left);
                Assert.Equal("set", view.EnteredInstance);
                Assert.Equal(revision, editor.Revision);

                // 안쪽의 것을 끈다 — 품은 세트의 오브젝트가 옮겨지고(한 칸) 인스턴스는 그대로다.
                var drop = new Vec2(grab.X + 60f, grab.Y + 30f);
                window.MouseDown(At(grab), MouseButton.Left);
                Assert.Equal("set.Color1", view.SelectedNode);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal((60f, 30f), (Color().X, Color().Y));
                Assert.Equal((300f, 100f), (Instance().X, Instance().Y));

                // Esc — 나온다. 그 인스턴스가 골라지고, 이제 안쪽을 끌면 인스턴스가 통째로 옮겨진다.
                view.Focus();
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Assert.Null(view.EnteredInstance);
                Assert.Equal("set", window.SetEditorState.Node);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                grab = Inner();
                drop = new Vec2(grab.X + 10f, grab.Y);
                window.MouseDown(At(grab), MouseButton.Left);
                window.MouseMove(At(drop), RawInputModifiers.LeftMouseButton);
                window.MouseUp(At(drop), MouseButton.Left);
                Assert.Equal((310f, 60f), (Instance().X, Color().X));

                // 칸의 단추로도 들어간다. 애니메이션을 고른 때(배치 모드가 아니다)는 안 들어가진다.
                window.SelectSetNode("set");
                Assert.True(window.EnterInstanceInPlaceFromForm());
                Assert.Equal("set", view.EnteredInstance);
                view.EnterInstance(null);
                editor.AddMotion("city_in", "set_2", 1f, MotionEnd.Commit);
                window.SelectMotion("city_in");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.SelectSetNode("set");
                Assert.False(window.EnterInstanceInPlaceFromForm());
                Assert.Null(view.EnteredInstance);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 컷에서_메뉴로_층을_새_세트로_빼고_이름을_바꿔도_타임라인의_재생이_그대로다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                ViewportView view = window.Viewport;

                // 컷에 층 표를 들이고, sign에 자리와 배율의 키를 놓는다(가만히 선 자리 위에서 움직인다).
                // 키는 처음(0초)과 끝(10초)에도 둔다 — 빼낸 뒤에는 세트의 애니메이션이라 "0초에서 시작해 길이에서 끝난다"는 규칙을 받는다.
                window.NewCutFromForm();
                window.ImportLayerSheetFromForm(TestDocuments.ArtPath("layers.tsv"));
                editor.AddChannel("cut", "sign", MotionChannelKind.Position);
                editor.SetKeys("cut", "sign", MotionChannelKind.Position, Axis.X, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(0.5f, 0f), TestDocuments.Key(1.5f, 240f), TestDocuments.Key(2.5f, -80f), TestDocuments.Key(10f, -80f) });
                editor.SetKeys("cut", "sign", MotionChannelKind.Position, Axis.Y, new[] { TestDocuments.Key(0f, 0f), TestDocuments.Key(2.5f, 60f), TestDocuments.Key(10f, 60f) });
                editor.AddChannel("cut", "sign", MotionChannelKind.Scale);
                editor.SetKeys("cut", "sign", MotionChannelKind.Scale, Axis.X, new[] { TestDocuments.Key(0f, 1f), TestDocuments.Key(1f, 1f), TestDocuments.Key(2f, 1.5f), TestDocuments.Key(10f, 1.5f) });
                editor.SetKeys("cut", "sign", MotionChannelKind.Scale, Axis.Y, new[] { TestDocuments.Key(0f, 1f), TestDocuments.Key(1f, 1f), TestDocuments.Key(2f, 1.5f), TestDocuments.Key(10f, 1.5f) });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, window.ProblemRowsShown);
                double[] times = { 0.2, 0.7, 1.0, 1.6, 2.0, 2.5, 2.9 };

                // 타임라인을 틀어 그 층의 자리와 크기를 적어 둔다.
                List<(Vec2 Origin, Vec2 Edge)> Sample(string node)
                {
                    var samples = new List<(Vec2, Vec2)>();

                    foreach (double t in times)
                    {
                        window.Graph.CurrentTime = t;
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        RectNodeTree pose = view.CurrentPose()!;
                        string key = $"{ViewportView.SetObjectId}/{node}";
                        samples.Add((pose.TransformPoint(key, Vec3.Zero).XY, pose.TransformPoint(key, new Vec3(100f, 0f, 0f)).XY));
                    }

                    return samples;
                }

                List<(Vec2 Origin, Vec2 Edge)> before = Sample("sign");
                Assert.True(Math.Abs(before[3].Origin.X - before[0].Origin.X) > 50f); // 키가 실제로 움직인다

                // 메뉴로 뺀다 → 새 세트를 열어 이름을 plate로. 두 칸.
                long revision = editor.Revision;
                Assert.Equal("sign", window.ExtractSetNodesFromMenu("sign"));
                window.SelectSet("sign");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.SetNameBox.Focus();
                window.SetNameBox.Text = "plate";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.Equal(revision + 2, editor.Revision);

                // 세트·애니메이션·스킨·인스턴스의 이름이 다 따라왔고, 컷의 타임라인이 그 애니메이션을 재생 블록으로 튼다.
                Assert.Equal("plate", editor.Document.FindCut("cut")!.Rig.Find("plate")!.Set);
                string moved = Assert.Single(editor.Document.Motions, m => m.Rig == "plate").Name;
                Assert.StartsWith("plate_", moved, StringComparison.Ordinal);
                Assert.NotNull(editor.Document.FindSkin("plate"));
                Assert.Contains(editor.Document.Find("cut")!.Plays, p => p.Node == "plate" && p.Motion == moved);
                Assert.Equal(0, window.ProblemRowsShown);

                // 컷의 재생은 그대로다.
                window.SelectSet("cut");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                List<(Vec2 Origin, Vec2 Edge)> after = Sample("plate.sign");

                for (int i = 0; i < times.Length; i++)
                {
                    Assert.True(Math.Abs(before[i].Origin.X - after[i].Origin.X) < 1.5f && Math.Abs(before[i].Origin.Y - after[i].Origin.Y) < 1.5f, $"자리 @ {times[i]}");
                    Assert.True(Math.Abs(before[i].Edge.X - after[i].Edge.X) < 1.5f && Math.Abs(before[i].Edge.Y - after[i].Edge.Y) < 1.5f, $"크기 @ {times[i]}");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void 하이어아키_메뉴의_화면_밖에서_들어온다는_지금_시각부터_자리_키를_놓는다()
    {
        HeadlessUi.Run(() =>
        {
            MainWindow window = NewWindow();
            window.Show();

            try
            {
                MotionEditor editor = window.Session.Editor;
                window.NewSetFromForm();
                window.SelectSetNode(null);
                window.AddColorNodeFromForm(); // Color1 — 400×300, 무대 가운데
                editor.AddMotion("set_in", "set", 1f, MotionEnd.Commit);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // 애니메이션을 고르지 않았으면 할 일이 없다.
                Assert.Null(window.SlideSetNodeFromMenu("Color1", StageSide.Left, leaving: false));

                // 0.25초에서 "왼쪽에서" — 한 칸. 무대의 반(960) + 너비의 반(200) = 1160만큼 왼쪽에서 0.5초 동안 들어온다.
                window.SelectMotion("set_in");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.Graph.CurrentTime = 0.25;
                long revision = editor.Revision;
                Assert.Equal(-1160f, window.SlideSetNodeFromMenu("Color1", StageSide.Left, leaving: false));
                Assert.Equal(revision + 1, editor.Revision);
                Assert.Equal(new[] { (0f, -1160f), (0.25f, -1160f), (0.75f, 0f), (1f, 0f) },
                    editor.Document.Find("set_in")!.FindChannel("Color1", MotionChannelKind.Position)!.X!.Keys.Select(k => (k.T, k.V)));
                Assert.Equal(0, window.ProblemRowsShown);

                // 뷰포트: 0.1초에는 무대 밖(왼쪽), 끝에는 제자리.
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                float X(double t)
                {
                    window.Graph.CurrentTime = t;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    return window.Viewport.CurrentPose()!.TransformPoint("set/Color1", Vec3.Zero).X;
                }

                Assert.Equal(-1160f, X(0.1) - X(1.0), 0);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
