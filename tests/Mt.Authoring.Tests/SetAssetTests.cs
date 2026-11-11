using System.Text.Json.Nodes;
using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Sets;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;
using Mt.Cli;

namespace Mt.Authoring.Tests;

/// <summary>툴에서 정의한 리그(<c>PLAN-parts.md</c> §9) — 세트와 컷의 오브젝트 계층. Spine처럼 툴이 원본.</summary>
public class SetAssetTests
{
    private static DocumentEditor NewEditor()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(PartsSetFixture.Set());
        return editor;
    }

    [Fact]
    public void 문서의_리그는_한_벌로_서고_조각과_불투명도_노드가_표시대로_나온다()
    {
        DocumentEditor editor = NewEditor();
        Assert.Empty(Fixtures.SetOf(new AuthoringDocument()).SetNames); // 빈 문서 — 아는 리그가 없다
        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Equal(new[] { PartsSetFixture.SetName }, set.SetNames);

        Assert.Equal(new[] { "Badge_Part1", "Badge_Part2", "Badge_Part3", "Badge_Part4" }, ObjectParts.Of(set, PartsSetFixture.SetName));
        Assert.Equal(new[] { "Badge_Body", "Badge_Part1", "Badge_Part2", "Badge_Part3", "Badge_Part4" }, set.AlphaNodeIds[PartsSetFixture.SetName].OrderBy(n => n, StringComparer.Ordinal));
        Assert.Contains("Badge_Anchor", set.NodeIds[PartsSetFixture.SetName]);

        // 트리에 서고, 조각은 정의한 크기다.
        RectNodeTree tree = set.BuildTree(PartsSetFixture.SetName, "e1/");
        Assert.Equal("e1/Badge_Body", tree.GetParentKey("e1/Badge_Part2"));
        Assert.Equal(PartsSetFixture.PartSize, tree.GetRectSize("e1/Badge_Part1").X, 3);

        // 조각은 이름이 아니라 표시(Image)로 가린다 — 이름 규칙에 안 맞아도 된다.
        editor.AddSet(new SetAsset { Name = "prop", Objects = { new ObjectAsset { Id = "Hand" }, new ObjectAsset { Id = "Leaf", Parent = "Hand", Width = 80, Height = 40, Image = true } } });
        BuiltSets both = Fixtures.SetOf(editor.Document);
        Assert.Equal(new[] { "Leaf" }, ObjectParts.Of(both, "prop"));
        Assert.Empty(both.AlphaNodeIds["prop"]);

        // 모션·스킨 검증이 이 리그의 노드로 선다.
        editor.AddTimeline("pop", PartsSetFixture.SetName, 0.2f);
        editor.AddChannel("pop", "Badge_Body", ChannelKind.Alpha);
        editor.SetKeys("pop", "Badge_Body", ChannelKind.Alpha, Axis.X, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.2f, 1f) });
        Assert.Empty(DocumentValidator.Validate(editor.Document, both.NodeIds, both.AlphaNodeIds));
        Assert.NotEmpty(DocumentValidator.Validate(editor.Document, Fixtures.ActorBuiltSets().NodeIds)); // 그 세트가 없는 한 벌로는 모르는 리그다
    }

    [Fact]
    public void 편집기는_세울_수_있는_리그만_받는다()
    {
        DocumentEditor editor = NewEditor();
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddSet(PartsSetFixture.Set()));                       // 이미 있다
        Assert.Throws<InvalidOperationException>(() => editor.AddSet(PartsSetFixture.Set("1bad")));
        Assert.Throws<InvalidOperationException>(() => editor.AddSet(new SetAsset { Name = "x", Objects = { new ObjectAsset { Id = "A", Parent = "B" }, new ObjectAsset { Id = "B" } } })); // 부모가 뒤에
        Assert.Throws<InvalidOperationException>(() => editor.AddSet(new SetAsset { Name = "x", Objects = { new ObjectAsset { Id = "A" }, new ObjectAsset { Id = "A" } } }));
        Assert.Throws<InvalidOperationException>(() => editor.AddSet(new SetAsset { Name = "x", Objects = { new ObjectAsset { Id = "a/b" } } }));
        Assert.Equal(revision, editor.Revision);

        // 받은 객체를 문서에 꽂지 않는다 — 밖에서 고쳐도 문서는 그대로.
        SetAsset given = PartsSetFixture.Set("prop");
        editor.AddSet(given);
        given.Objects.Clear();
        Assert.Equal(6, editor.Document.FindSetOrCut("prop")!.Objects.Count);

        // 저작본 왕복. 리그가 없는 문서에는 안 적힌다.
        string json = AuthoringDocumentJson.Write(editor.Document);
        Assert.Equal(json, AuthoringDocumentJson.Write(AuthoringDocumentJson.Read(json)));
        Assert.DoesNotContain("\"sets\"", AuthoringDocumentJson.Write(new AuthoringDocument()), StringComparison.Ordinal);

        // 지우면 한 벌에서 빠진다.
        editor.RemoveSet(PartsSetFixture.SetName);
        Assert.DoesNotContain(PartsSetFixture.SetName, Fixtures.SetOf(editor.Document).SetNames);

        // 문서가 정의한 것만의 한 벌(앱이 쓰는 길) — 무대 크기를 받는다.
        BuiltSets alone = SetBuilder.Compose(editor.Document, new Vec2(1920f, 1080f));
        Assert.Equal(new[] { "prop" }, alone.SetNames);
        Assert.Equal(1920f, alone.BuildTree("prop").GetRectSize(HierarchyLoader.RootKey).X, 3);
        Assert.Empty(SetBuilder.Compose(new AuthoringDocument(), new Vec2(1920f, 1080f)).SetNames);
    }

    [Fact]
    public void 명령줄도_문서의_리그로_검증하고_포즈를_읽는다()
    {
        DocumentEditor editor = NewEditor();
        editor.AddTimeline("pop", PartsSetFixture.SetName, 0.2f);
        editor.AddChannel("pop", "Badge_Body", ChannelKind.Position);
        editor.SetKeys("pop", "Badge_Body", ChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.2f, 40f) });

        string directory = Path.Combine(Path.GetTempPath(), "ked-timeline-tool-tests", $"set-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string document = Path.Combine(directory, "a.cut-authoring.json");
        File.WriteAllText(document, AuthoringDocumentJson.Write(editor.Document));

        int Run(out JsonNode? json, params string[] args)
        {
            var stdout = new StringWriter();
            int code = MtCli.Run(args, stdout, new StringWriter());
            json = JsonNode.Parse(stdout.ToString());
            return code;
        }

        Assert.Equal(0, Run(out JsonNode? validated, "validate", document)); // 문서의 리그로 통과한다
        Assert.Empty(validated!["problems"]!.AsArray());
        Assert.Equal(0, Run(out JsonNode? pose, "pose", document, "--timeline", "pop", "--time", "0.2"));
        Assert.Equal(40.0, pose!["nodes"]!["Badge_Body"]!["position"]![1]!.GetValue<double>(), 3);
        Assert.Equal(PartsSetFixture.SetName, pose["set"]!.GetValue<string>());
    }

    // ── 리그 편집기(PLAN-parts.md §12) ─────────────────────────────────

    private static string[] Ids(DocumentEditor editor, string kind = PartsSetFixture.SetName) => editor.Document.FindSetOrCut(kind)!.Objects.Select(n => n.Id).ToArray();

    [Fact]
    public void 노드를_짓고_지우고_옮기면_부모가_늘_자식보다_먼저_온다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(PartsSetFixture.Set(parts: 2));
        long revision = editor.Revision;

        // 더하기 — 부모의 식구들 맨 뒤. 그림 노드는 본의 크기와 불투명도를 갖고 선다.
        ObjectAsset glow = editor.AddObject(PartsSetFixture.SetName, "Badge_Glow", "Badge_Anchor", image: true);
        Assert.Equal((160f, 160f, true, true), (glow.Width, glow.Height, glow.Image, glow.Alpha));
        editor.AddObject(PartsSetFixture.SetName, "Badge_Spark", "Badge_Glow");
        editor.AddObject(PartsSetFixture.SetName, "Extra"); // 루트 밑
        Assert.Equal(revision + 3, editor.Revision);
        Assert.Equal(new[] { "Badge_Anchor", "Badge_Body", "Badge_Part1", "Badge_Part2", "Badge_Glow", "Badge_Spark", "Extra" }, Ids(editor));

        // 막는 것 — 기록이 안 남는다.
        revision = editor.Revision;
        Assert.Throws<InvalidOperationException>(() => editor.AddObject(PartsSetFixture.SetName, "Badge_Glow"));       // 이미 있다
        Assert.Throws<InvalidOperationException>(() => editor.AddObject(PartsSetFixture.SetName, "1bad"));
        Assert.Throws<InvalidOperationException>(() => editor.AddObject(PartsSetFixture.SetName, "a/b"));
        Assert.Throws<KeyNotFoundException>(() => editor.AddObject(PartsSetFixture.SetName, "X", "Nope"));
        Assert.Throws<KeyNotFoundException>(() => editor.AddObject("character", "X"));                                 // 문서에 없는 세트는 못 고친다
        Assert.Throws<InvalidOperationException>(() => editor.SetObjectParent(PartsSetFixture.SetName, "Badge_Glow", "Badge_Spark")); // 제 밑으로
        Assert.Throws<InvalidOperationException>(() => editor.UpdateObject(PartsSetFixture.SetName, "Extra", new ObjectAsset { Width = -1f }));
        Assert.Equal(revision, editor.Revision);

        // 형제 사이의 순서 — 식구째 옮긴다. 끝에서는 아무 일도 없다.
        editor.MoveObject(PartsSetFixture.SetName, "Badge_Glow", -1);
        Assert.Equal(new[] { "Badge_Anchor", "Badge_Glow", "Badge_Spark", "Badge_Body", "Badge_Part1", "Badge_Part2", "Extra" }, Ids(editor));
        editor.MoveObject(PartsSetFixture.SetName, "Badge_Glow", -1);
        editor.MoveObject(PartsSetFixture.SetName, "Badge_Part2", +1);
        Assert.Equal(revision + 1, editor.Revision);
        editor.MoveObject(PartsSetFixture.SetName, "Badge_Anchor", +1);
        Assert.Equal(new[] { "Extra", "Badge_Anchor", "Badge_Glow", "Badge_Spark", "Badge_Body", "Badge_Part1", "Badge_Part2" }, Ids(editor));

        // 부모 바꾸기 — 식구째, 새 부모의 맨 뒤로. 같은 부모면 무기록.
        revision = editor.Revision;
        editor.SetObjectParent(PartsSetFixture.SetName, "Badge_Glow", "Badge_Body");
        editor.SetObjectParent(PartsSetFixture.SetName, "Badge_Glow", "Badge_Body");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { "Extra", "Badge_Anchor", "Badge_Body", "Badge_Part1", "Badge_Part2", "Badge_Glow", "Badge_Spark" }, Ids(editor));
        Assert.Equal("Badge_Body", editor.Document.FindSetOrCut(PartsSetFixture.SetName)!.Find("Badge_Glow")!.Parent);
        editor.SetObjectParent(PartsSetFixture.SetName, "Badge_Body", null); // 루트로
        Assert.Equal(new[] { "Extra", "Badge_Anchor", "Badge_Body", "Badge_Part1", "Badge_Part2", "Badge_Glow", "Badge_Spark" }, Ids(editor));
        Assert.Equal("", editor.Document.FindSetOrCut(PartsSetFixture.SetName)!.Find("Badge_Body")!.Parent);

        // 값 — 한 칸, 같은 값은 무기록. 세운 나무에 그대로 선다.
        revision = editor.Revision;
        var moved = new ObjectAsset { X = 30f, Y = -12f, Width = 80f, Height = 40f, Image = true, Alpha = false };
        editor.UpdateObject(PartsSetFixture.SetName, "Badge_Glow", moved);
        editor.UpdateObject(PartsSetFixture.SetName, "Badge_Glow", moved);
        Assert.Equal(revision + 1, editor.Revision);
        BuiltSets composed = Fixtures.SetOf(editor.Document);
        Assert.True(composed.TryGetSet(PartsSetFixture.SetName, out HierarchyDto dto));
        HierarchyNodeDto node = dto.nodes.Single(n => n.id == "Badge_Glow");
        Assert.Equal((30f, -12f, 80f, 40f, true, false), (node.anchoredPosition.x, node.anchoredPosition.y, node.sizeDelta.x, node.sizeDelta.y, node.hasImage, node.hasAlpha));
        Assert.Contains("Badge_Glow", ObjectParts.Of(composed, PartsSetFixture.SetName));

        // 가만히 선 자세의 회전·배율(배치 모드) — 세운 나무에 그대로 선다. 배율 0은 막는다.
        editor.UpdateObject(PartsSetFixture.SetName, "Badge_Glow", new ObjectAsset { X = 30f, Y = -12f, Width = 80f, Height = 40f, Image = true, Rotation = 30f, ScaleX = 2f, ScaleY = -1f });
        Assert.True(Fixtures.SetOf(editor.Document).TryGetSet(PartsSetFixture.SetName, out HierarchyDto posed));
        HierarchyNodeDto turned = posed.nodes.Single(n => n.id == "Badge_Glow");
        Assert.Equal((30f, 2f, -1f), (turned.localEulerAngles.z, turned.localScale.x, turned.localScale.y));
        Assert.Throws<InvalidOperationException>(() => editor.UpdateObject(PartsSetFixture.SetName, "Badge_Glow", new ObjectAsset { ScaleX = 0f }));
        editor.Undo();

        // 지우기 — 밑의 노드도 함께. 되돌리면 그 자리에 돌아온다.
        editor.RemoveObject(PartsSetFixture.SetName, "Badge_Body");
        Assert.Equal(new[] { "Extra", "Badge_Anchor" }, Ids(editor));
        editor.Undo();
        Assert.Equal(7, Ids(editor).Length);
        Fixtures.SetOf(editor.Document); // 여전히 세울 수 있다
    }

    [Fact]
    public void 노드의_이름을_바꾸면_채널과_스킨이_따라온다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(PartsSetFixture.Set(parts: 2));
        editor.AddSkin("땀", PartsSetFixture.SetName);
        editor.SetSkinPart("땀", "Badge_Part1", "art/sweat.png");
        editor.AddTimeline("pop", PartsSetFixture.SetName, 0.2f);
        editor.AddChannel("pop", "Badge_Part1", ChannelKind.Position);
        editor.AddChannel("pop", "Badge_Body", ChannelKind.Position);

        long revision = editor.Revision;
        editor.RenameObject(PartsSetFixture.SetName, "Badge_Part1", "Drop");
        editor.RenameObject(PartsSetFixture.SetName, "Badge_Anchor", "Pin");
        editor.RenameObject(PartsSetFixture.SetName, "Pin", "Pin"); // 같은 이름은 무기록
        Assert.Equal(revision + 2, editor.Revision);

        Assert.Equal(new[] { "Pin", "Badge_Body", "Drop", "Badge_Part2" }, Ids(editor));
        Assert.Equal("Pin", editor.Document.FindSetOrCut(PartsSetFixture.SetName)!.Find("Badge_Body")!.Parent);
        Assert.Equal(new[] { "Drop", "Badge_Body" }, editor.Document.Find("pop")!.Channels.Select(c => c.Object));
        Assert.Equal("art/sweat.png", editor.Document.FindSkin("땀")!.Parts["Drop"]);
        Assert.False(editor.Document.FindSkin("땀")!.Parts.ContainsKey("Badge_Part1"));

        // 이름이 겹치거나 규칙에 안 맞으면 막는다. 되돌리면 전부 함께 돌아온다.
        Assert.Throws<InvalidOperationException>(() => editor.RenameObject(PartsSetFixture.SetName, "Drop", "Badge_Body"));
        Assert.Throws<InvalidOperationException>(() => editor.RenameObject(PartsSetFixture.SetName, "Drop", "물방울"));
        editor.Undo();
        editor.Undo();
        Assert.Equal("Badge_Part1", editor.Document.Find("pop")!.Channels[0].Object);
        Assert.True(editor.Document.FindSkin("땀")!.Parts.ContainsKey("Badge_Part1"));
    }

    [Fact]
    public void 색_오브젝트는_세운_리그에_fillColor로_서고_색_글은_고른_꼴로_선다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(new SetAsset { Name = "wipe", Objects = { new ObjectAsset { Id = "Body", Alpha = true } } });
        editor.AddObject("wipe", "Panel", "Body");
        long revision = editor.Revision;

        // 색을 주면 색 오브젝트 — 한 칸. "#" 없이, 소문자로 적어도 고른 꼴로 선다. 같은 값은 무기록.
        editor.UpdateObject("wipe", "Panel", new ObjectAsset { Width = 1920f, Height = 400f, Color = "3d5a80", Alpha = true });
        editor.UpdateObject("wipe", "Panel", new ObjectAsset { Width = 1920f, Height = 400f, Color = "#3D5A80", Alpha = true });
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal("#3D5A80", editor.Document.FindSetOrCut("wipe")!.Find("Panel")!.Color);
        Assert.Throws<InvalidOperationException>(() => editor.UpdateObject("wipe", "Panel", new ObjectAsset { Color = "blue" }));
        Assert.Equal("#11223344", DocumentEditor.NormalizeColor(" #11223344 ")); // 불투명도까지
        Assert.Null(DocumentEditor.NormalizeColor("  "));

        // 세운 리그: 색 표에 그 노드가 서고, 그림 노드는 아니다(조각이 아니다).
        BuiltSets composed = Fixtures.SetOf(editor.Document);
        Assert.Equal("#3D5A80", ObjectParts.ColorsOf(composed, "wipe")["Panel"]);
        Assert.Empty(ObjectParts.Of(composed, "wipe"));
        Assert.Empty(ObjectParts.ColorsOf(composed, "nope"));
        Assert.True(composed.TryGetSet("wipe", out HierarchyDto wipe));
        Assert.Equal(new[] { ("Body", ""), ("Panel", "#3D5A80") }, wipe.nodes.Where(n => n.id != HierarchyLoader.RootKey).Select(n => (n.id, n.fillColor ?? "")));

        // 저작본 — 색이 없는 노드에는 안 적힌다.
        string json = AuthoringDocumentJson.Write(editor.Document);
        Assert.Equal(1, json.Split("\"color\"").Length - 1);
        Assert.Equal("#3D5A80", AuthoringDocumentJson.Read(json).FindSetOrCut("wipe")!.Find("Panel")!.Color);

        // 비우면 색 오브젝트가 아니다.
        editor.UpdateObject("wipe", "Panel", new ObjectAsset { Width = 1920f, Height = 400f, Alpha = true });
        Assert.Null(editor.Document.FindSetOrCut("wipe")!.Find("Panel")!.Color);
        Assert.Empty(ObjectParts.ColorsOf(Fixtures.SetOf(editor.Document), "wipe"));
    }

    // ── 시차 — 오브젝트의 움직임을 통째로 민다 ─────────────────────────

    [Fact]
    public void 움직임을_밀면_길이는_그대로이고_비는_쪽은_가만히_넘치는_쪽은_잘린다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(PartsSetFixture.Set(parts: 3));
        editor.AddTimeline("in", PartsSetFixture.SetName, 1f);
        KeyAsset Lin(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Linear };

        foreach (string part in new[] { "Badge_Part1", "Badge_Part2", "Badge_Part3" })
        {
            editor.AddChannel("in", part, ChannelKind.Position);
            editor.SetKeys("in", part, ChannelKind.Position, Axis.X, new[] { Lin(0f, -100f), Lin(0.2f, -100f), Lin(0.6f, 0f), Lin(1f, 0f) }); // 0.2~0.6초에 들어온다
        }

        (float T, float V)[] Keys(string part) => editor.Document.Find("in")!.FindChannel(part, ChannelKind.Position)!.X!.Keys.Select(k => (MathF.Round(k.T, 3), MathF.Round(k.V, 1))).ToArray();
        long revision = editor.Revision;

        // 0.1초 늦게 — 안쪽 키가 밀리고 처음과 끝은 제 값으로 선다. 한 칸.
        Assert.True(editor.ShiftNodeKeys("in", "Badge_Part1", 0.1f));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { (0f, -100f), (0.3f, -100f), (0.7f, 0f), (1f, 0f) }, Keys("Badge_Part1"));

        // 넘치게 늦추면 잘린다 — 끝의 키는 잘린 자리의 값(아직 들어오는 중).
        editor.ShiftNodeKeys("in", "Badge_Part2", 0.6f);
        Assert.Equal(new[] { (0f, -100f), (0.8f, -100f), (1f, -50f) }, Keys("Badge_Part2"));

        // 일찍 — 처음의 키는 잘린 자리의 값(이미 들어오는 중), 끝은 마지막 값으로 가만히.
        editor.ShiftNodeKeys("in", "Badge_Part3", -0.4f);
        Assert.Equal(new[] { (0f, -50f), (0.2f, 0f), (1f, 0f) }, Keys("Badge_Part3"));

        // 어느 쪽이든 문제가 없다.
        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));

        // 채널이 없는 오브젝트·0초는 아무 일도 없다(기록 없음).
        revision = editor.Revision;
        Assert.False(editor.ShiftNodeKeys("in", "Badge_Body", 0.1f));
        Assert.False(editor.ShiftNodeKeys("in", "Badge_Part1", 0f));
        Assert.Equal(revision, editor.Revision);

        // 시차 — 순서대로 0.1초씩 더: 첫째는 그대로. 통째로 한 칸.
        editor.Undo();
        editor.Undo();
        editor.Undo();
        revision = editor.Revision;
        Assert.True(editor.StaggerNodes("in", new[] { "Badge_Part1", "Badge_Part2", "Badge_Part3" }, 0.1f));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(0.2f, Keys("Badge_Part1")[1].T);
        Assert.Equal(0.3f, Keys("Badge_Part2")[1].T);
        Assert.Equal(0.4f, Keys("Badge_Part3")[1].T);
    }

    // ── 제 차례 전에는 숨긴다 ───────────────────────────────────────────

    [Fact]
    public void 제_차례_전까지_숨기면_움직이기_시작하는_시각에_나타나는_불투명도_키가_놓인다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(PartsSetFixture.Set(parts: 3));
        editor.AddTimeline("in", PartsSetFixture.SetName, 1f);
        KeyAsset Lin(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Linear };
        editor.AddChannel("in", "Badge_Part1", ChannelKind.Position); // 0.4초에 움직이기 시작해 0.8초에 선다
        editor.SetKeys("in", "Badge_Part1", ChannelKind.Position, Axis.X, new[] { Lin(0f, -100f), Lin(0.4f, -100f), Lin(0.8f, 0f), Lin(1f, 0f) });
        editor.AddChannel("in", "Badge_Part2", ChannelKind.Position); // 처음부터 끝까지 움직인다
        editor.SetKeys("in", "Badge_Part2", ChannelKind.Position, Axis.X, new[] { Lin(0f, -100f), Lin(1f, 0f) });
        (float T, float V)[] Alpha(string part) => editor.Document.Find("in")!.FindChannel(part, ChannelKind.Alpha)!.X!.Keys.Select(k => (MathF.Round(k.T, 3), k.V)).ToArray();

        // 제 차례(0.4초) 전까지 0, 0.1초에 걸쳐 나타난다 — 한 칸.
        long revision = editor.Revision;
        Assert.True(editor.HideUntilTurn("in", "Badge_Part1"));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { (0f, 0f), (0.4f, 0f), (0.5f, 1f), (1f, 1f) }, Alpha("Badge_Part1"));

        // 움직임을 마치면(0.8초) 숨긴다 — 있던 불투명도 커브를 갈아 치운다.
        Assert.True(editor.HideAfterTurn("in", "Badge_Part1"));
        Assert.Equal(new[] { (0f, 1f), (0.7f, 1f), (0.8f, 0f), (1f, 0f) }, Alpha("Badge_Part1"));

        // 처음부터(끝까지) 움직이거나 움직임이 없는 오브젝트는 할 일이 없다(기록 없음).
        revision = editor.Revision;
        Assert.False(editor.HideUntilTurn("in", "Badge_Part2"));
        Assert.False(editor.HideAfterTurn("in", "Badge_Part2"));
        Assert.False(editor.HideUntilTurn("in", "Badge_Part3"));
        Assert.Equal(revision, editor.Revision);

        // 전부 — 움직이는 오브젝트 가운데 늦게 나오는 것만, 한 칸. 규칙에 맞는다.
        editor.Undo();
        editor.Undo();
        revision = editor.Revision;
        Assert.Equal(1, editor.HideAllUntilTurn("in"));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Null(editor.Document.Find("in")!.FindChannel("Badge_Part2", ChannelKind.Alpha));
        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));
    }

    // ── 세트가 세트를 품는다(PLAN-set.md S9) ───────────────────────────

    /// <summary>car(Body › Shell(그림) · Wheel(색)) · sun(Disc(그림)) · city(Ground(색) + car 인스턴스 + sun 인스턴스).</summary>
    private static DocumentEditor NestedEditor()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(new SetAsset { Name = "car", Objects = { new ObjectAsset { Id = "Body", Alpha = true } } });
        editor.AddObject("car", "Shell", "Body", image: true);
        editor.AddObject("car", "Wheel", "Body");
        editor.UpdateObject("car", "Wheel", new ObjectAsset { X = 30f, Y = -40f, Width = 20f, Height = 20f, Color = "#222222", Alpha = true });
        editor.AddSkin("car_red", "car");
        editor.SetSkinPart("car_red", "Shell", "art/car_red.png");
        editor.AddSkin("car_blue", "car");
        editor.SetSkinPart("car_blue", "Shell", "art/car_blue.png");

        editor.AddSet(new SetAsset { Name = "sun", Objects = { new ObjectAsset { Id = "Disc", Width = 100f, Height = 100f, Image = true, Alpha = true } } });
        editor.AddSkin("sun", "sun");
        editor.SetSkinPart("sun", "Disc", "art/sun.png");

        editor.AddSet(new SetAsset { Name = "city", Objects = { new ObjectAsset { Id = "Ground", Width = 1920f, Height = 200f, Color = "#334455", Alpha = true } } });
        return editor;
    }

    [Fact]
    public void 세트를_품으면_그_오브젝트들이_경로_이름으로_펼쳐져_한_덩어리로_선다()
    {
        DocumentEditor editor = NestedEditor();
        long revision = editor.Revision;

        // 품는다 — 한 칸. 인스턴스 오브젝트는 통째로 옮기는 손잡이다(자리 · 배율).
        ObjectAsset car = editor.AddInstance("city", "car", "car");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(("car", true), (car.Set, car.Alpha));
        editor.UpdateObject("city", "car", new ObjectAsset { X = 300f, Y = 50f, ScaleX = 2f, ScaleY = 2f, Alpha = true });
        Assert.Equal("car", editor.Document.FindSetOrCut("city")!.Find("car")!.Set); // 값을 고쳐도 품은 것은 그대로다
        editor.AddInstance("city", "sun", "sun");

        // 세운 리그: 품은 세트가 인스턴스 밑에 "인스턴스.이름"으로 펼쳐진다. 부모·색·그림 표시가 따라온다.
        BuiltSets composed = Fixtures.SetOf(editor.Document);
        Assert.True(composed.TryGetSet("city", out HierarchyDto dto));
        Assert.Equal(new[] { "__root", "Ground", "car", "car.Body", "car.Shell", "car.Wheel", "sun", "sun.Disc" }, dto.nodes.Select(n => n.id));
        Assert.Equal(("car", "car.Body", "car.Body"), (dto.nodes.Single(n => n.id == "car.Body").parent, dto.nodes.Single(n => n.id == "car.Shell").parent, dto.nodes.Single(n => n.id == "car.Wheel").parent));
        Assert.Equal(new[] { "car.Shell", "sun.Disc" }, ObjectParts.Of(composed, "city"));
        Assert.Equal("#222222", ObjectParts.ColorsOf(composed, "city")["car.Wheel"]);
        Assert.Equal("car", SetCodec.OwnerOf("car.Wheel"));

        // 선 자리: 인스턴스의 자리·배율이 안쪽에 곱해진다 — 바퀴는 (300 + 30×2, 50 − 40×2).
        RectNodeTree tree = composed.BuildTree("city", "c/");
        Vec2 root = tree.TransformPoint("c/__root", Vec3.Zero).XY;
        Vec2 wheel = tree.TransformPoint("c/car.Wheel", Vec3.Zero).XY;
        Assert.Equal((360f, -30f), (MathF.Round(wheel.X - root.X, 1), MathF.Round(wheel.Y - root.Y, 1)));

        // 실제로 입는 스킨: 품은 세트의 스킨이 경로 이름으로 — 인스턴스가 고른 것, 없으면 그 세트의 첫 스킨.
        SkinAsset worn = SetBuilder.EffectiveSkin(editor.Document, "city", null)!;
        Assert.Equal(("art/car_red.png", "art/sun.png"), (worn.Parts["car.Shell"], worn.Parts["sun.Disc"]));
        editor.SetInstanceSkin("city", "car", "car_blue");
        Assert.Equal("art/car_blue.png", SetBuilder.EffectiveSkin(editor.Document, "city", null)!.Parts["car.Shell"]);
        Assert.Throws<InvalidOperationException>(() => editor.SetInstanceSkin("city", "car", "sun"));     // 다른 세트의 스킨
        Assert.Throws<InvalidOperationException>(() => editor.SetInstanceSkin("city", "Ground", "sun"));  // 품은 오브젝트가 아니다
        Assert.Same(editor.Document.FindSkin("sun"), SetBuilder.EffectiveSkin(editor.Document, "sun", "sun")); // 품은 것이 없으면 제 스킨 그대로

        // 참조다 — 원본 세트를 고치면 품은 곳이 따라온다.
        editor.AddObject("car", "Light", "Body");
        Assert.True(Fixtures.SetOf(editor.Document).TryGetSet("city", out HierarchyDto after));
        Assert.Contains(after.nodes, n => n.id == "car.Light");

        // 품은 것의 애니메이션: 인스턴스를 통째로 움직이는 키도, 안쪽의 것을 경로 이름으로 짚는 키도 놓인다 — 검증을 지난다.
        editor.AddTimeline("city_in", "city", 1f);
        editor.AddChannel("city_in", "car", ChannelKind.Position);
        editor.SetKeys("city_in", "car", ChannelKind.Position, Axis.X, new[] { new KeyAsset { T = 0f, V = -500f, Tangent = TangentMode.Linear }, new KeyAsset { T = 1f, V = 0f, Tangent = TangentMode.Linear } });
        editor.AddChannel("city_in", "car.Wheel", ChannelKind.Euler);
        editor.SetKeys("city_in", "car.Wheel", ChannelKind.Euler, Axis.Z, new[] { new KeyAsset { T = 0f, V = 0f, Tangent = TangentMode.Linear }, new KeyAsset { T = 1f, V = -360f, Tangent = TangentMode.Linear } });
        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));

        // 저작본은 참조만 적는다. 컷 파일에는 펼친 한 덩어리로 나간다(게임은 품는다는 것을 모른다) — 이 세트를 품은 컷으로 본다.
        string json = AuthoringDocumentJson.Write(editor.Document);
        var holder = new DocumentEditor(AuthoringDocumentJson.Read(json));
        holder.AddCut("shot");
        holder.AddInstance("shot", "town", "city");
        CutFileDto file = CutFileJson.Build(holder.Document, holder.Document.FindCut("shot")!, new Vec2(1920f, 1080f));
        Assert.Contains(file.objects, o => o.id == "town.car.Wheel" && o.parent == "town.car.Body" && o.color == "#222222");
        Assert.Contains("\"set\": \"car\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("car.Wheel\",\n          \"parent", json, StringComparison.Ordinal);
        Assert.Equal(json, AuthoringDocumentJson.Write(AuthoringDocumentJson.Read(json)));
    }

    [Fact]
    public void 서로_품는_것과_품긴_세트를_지우는_것은_막는다()
    {
        DocumentEditor editor = NestedEditor();
        editor.AddInstance("city", "car", "car");
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddInstance("city", "self", "city"));  // 자기 자신
        Assert.Throws<InvalidOperationException>(() => editor.AddInstance("car", "back", "city"));   // city가 car를 품고 있다
        editor.AddInstance("sun", "inner", "car");                                                     // sun → car는 된다
        Assert.Throws<InvalidOperationException>(() => editor.AddInstance("car", "far", "sun"));     // 두 다리 건너도 막는다
        Assert.Throws<KeyNotFoundException>(() => editor.AddInstance("city", "ghost", "nope"));
        Assert.Throws<InvalidOperationException>(() => editor.RemoveSet("car"));                          // 품긴 세트는 못 지운다
        Assert.Equal(revision + 1, editor.Revision);

        // 품은 것이 또 품은 것 — 경로가 이어진다.
        editor.AddInstance("city", "sun", "sun");
        Assert.True(Fixtures.SetOf(editor.Document).TryGetSet("city", out HierarchyDto dto));
        Assert.Contains(dto.nodes, n => n.id == "sun.inner.Wheel");

        // 인스턴스를 빼면 지울 수 있다.
        editor.RemoveObject("city", "car");
        editor.RemoveObject("sun", "inner");
        editor.RemoveSet("car");
        Assert.Null(editor.Document.FindSetOrCut("car"));

        // 손으로 쓴 문서의 순환·없는 세트는 펼칠 때 건너뛴다(던지지 않는다).
        var broken = new AuthoringDocument();
        broken.Sets.Add(new SetAsset { Name = "a", Objects = { new ObjectAsset { Id = "x", Set = "b" }, new ObjectAsset { Id = "y", Set = "ghost" } } });
        broken.Sets.Add(new SetAsset { Name = "b", Objects = { new ObjectAsset { Id = "z", Set = "a" } } });
        Assert.True(Fixtures.SetOf(broken).TryGetSet("a", out HierarchyDto looped));
        Assert.Equal(new[] { "__root", "x", "x.z", "y" }, looped.nodes.Select(n => n.id));
    }

    // ── 재생 블록 — 품은 세트의 애니메이션을 튼다(PLAN-set.md S10·S11) ───

    private static KeyAsset Lin(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Linear };

    /// <summary>
    /// <see cref="NestedEditor"/>에 더해: city가 car를 (300, 50)에 품고,
    /// car의 애니메이션 roll(0.4초: Body x 20 → 100) · hop(0.2초: Body y 0 → 30 → 0, 0.1초에 이벤트 thud) · spin(0.5초: Wheel 회전 0 → 90),
    /// city의 애니메이션 city_in(2초: Ground y −200 → 0). car의 Body는 가만히 선 자리가 (0, 0)이다.
    /// </summary>
    private static DocumentEditor PlayEditor()
    {
        DocumentEditor editor = NestedEditor();
        editor.AddInstance("city", "car", "car");
        editor.UpdateObject("city", "car", new ObjectAsset { X = 300f, Y = 50f, Alpha = true });

        editor.AddTimeline("roll", "car", 0.4f);
        editor.AddChannel("roll", "Body", ChannelKind.Position);
        editor.SetKeys("roll", "Body", ChannelKind.Position, Axis.X, new[] { Lin(0f, 20f), Lin(0.4f, 100f) });

        editor.AddTimeline("hop", "car", 0.2f);
        editor.AddChannel("hop", "Body", ChannelKind.Position);
        editor.SetKeys("hop", "Body", ChannelKind.Position, Axis.Y, new[] { Lin(0f, 0f), Lin(0.1f, 30f), Lin(0.2f, 0f) });
        editor.AddEventKey("hop", 0.1f, "thud");

        editor.AddTimeline("spin", "car", 0.5f);
        editor.AddChannel("spin", "Wheel", ChannelKind.Euler);
        editor.SetKeys("spin", "Wheel", ChannelKind.Euler, Axis.Z, new[] { Lin(0f, 0f), Lin(0.5f, 90f) });

        editor.AddTimeline("city_in", "city", 2f);
        editor.AddChannel("city_in", "Ground", ChannelKind.Position);
        editor.SetKeys("city_in", "Ground", ChannelKind.Position, Axis.Y, new[] { Lin(0f, -200f), Lin(2f, 0f) });
        return editor;
    }

    /// <summary>그 시각에 그 오브젝트가 가만히 선 자리에서 떨어진 만큼(Body는 가만히 선 자리가 (0, 0)이라 곧 그 자리다).</summary>
    private static Vec2 PosedAt(DocumentEditor editor, string node, double t)
    {
        BuiltSets set = Fixtures.SetOf(editor.Document);
        RectNodeTree template = set.BuildTree("city", "c/");
        RectNodeTree posed = TimelinePose.Pose(template, editor.Document.Find("city_in")!, t, null, "c/");
        return posed.GetState("c/" + node).AnchoredPosition - template.GetState("c/" + node).AnchoredPosition;
    }

    [Fact]
    public void 재생_블록은_품은_세트의_애니메이션을_그_시각에_틀고_끝나면_마지막_값이_남으며_되풀이는_처음부터_다시_튼다()
    {
        DocumentEditor editor = PlayEditor();
        long revision = editor.Revision;

        // 0.5초에 roll을 두 번, 1.5초에 hop. 각각 한 칸, 시작 시각 순으로 선다.
        Assert.Equal(0, editor.AddPlayBlock("city_in", "car", "hop", 1.5f));
        Assert.Equal(0, editor.AddPlayBlock("city_in", "car", "roll", 0.5f, repeat: 2));
        Assert.Equal(revision + 2, editor.Revision);
        TimelineAsset timeline = editor.Document.Find("city_in")!;
        Assert.Equal(new[] { ("roll", 0.5f), ("hop", 1.5f) }, timeline.Plays.Select(p => (p.Timeline, p.At)));

        // 구운 것: 제 채널(Ground) + 품은 것의 채널이 경로 이름으로. 저작본에는 재생 블록만 적힌다.
        Assert.NotNull(timeline.Flattened);
        Assert.Equal(new[] { "Ground", "car.Body" }, timeline.Flattened!.Channels.Select(c => c.Object).Distinct());
        string json = AuthoringDocumentJson.Write(editor.Document);
        Assert.Contains("\"plays\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("car.Body", json, StringComparison.Ordinal);
        Assert.NotNull(AuthoringDocumentJson.Read(json).Find("city_in")!.Flattened); // 읽으면 다시 굽는다

        // 첫 블록 앞: 가만히 선 값(0) — 블록의 첫 키 값(20)이 아니다. 블록이 시작하는 순간 그 값으로 간다(짧은 선으로 잇는다).
        Assert.Equal(0f, PosedAt(editor, "car.Body", 0.0).X, 1);
        Assert.Equal(0f, PosedAt(editor, "car.Body", 0.3).X, 1);
        Assert.Equal(0f, PosedAt(editor, "car.Body", 0.49).X, 1);
        Assert.Equal(20f, PosedAt(editor, "car.Body", 0.5).X, 1);

        // 블록 안: 품은 애니메이션의 키 그대로(첫 바퀴 0.5~0.9: 20 → 100).
        // 바로 뒤에 둘째 바퀴가 붙어 있어 첫 바퀴의 끝 키가 이음(1/240초)만큼 당겨진다 — 그래서 가운데가 아주 조금 다르다(60.4).
        Assert.Equal(60f, PosedAt(editor, "car.Body", 0.7).X, 0);
        Assert.InRange(PosedAt(editor, "car.Body", 0.9 - 2 * TimelineFlatten.Seam).X, 98f, 100f);

        // 되풀이: 둘째 바퀴(0.9~1.3)는 처음부터 다시 튼다 — 쌓이지 않는다(180이 아니라 다시 20 → 100).
        // 블록(바퀴)의 키는 제 시각에 정확히 선다 — 이음은 늘 그 앞쪽에 있다.
        Assert.Equal(20f, PosedAt(editor, "car.Body", 0.9).X, 1);
        Assert.Equal(60f, PosedAt(editor, "car.Body", 1.1).X, 1);
        Assert.Equal(100f, PosedAt(editor, "car.Body", 1.3).X, 1);

        // 블록이 끝난 뒤: 마지막 값(100)이 남는다 — 가만히 선 값으로 돌아가지 않는다. hop은 x에 키가 없으니 그 블록을 지나도 그대로다.
        Assert.Equal(100f, PosedAt(editor, "car.Body", 1.4).X, 1);
        Assert.Equal(100f, PosedAt(editor, "car.Body", 1.6).X, 1);
        Assert.Equal(100f, PosedAt(editor, "car.Body", 2.0).X, 1);

        // 구운 커브의 키 그대로: 가만히 선 값 … (이음) 첫 바퀴 … (이음) 둘째 바퀴 … 끝까지 머문다.
        float seam = TimelineFlatten.Seam;
        Assert.Equal(
            new[] { (0f, 0f), (0.5f - seam, 0f), (0.5f, 20f), (0.9f - seam, 100f), (0.9f, 20f), (1.3f, 100f), (2f, 100f) }.Select(k => (MathF.Round(k.Item1, 4), k.Item2)),
            timeline.Flattened.FindChannel("car.Body", ChannelKind.Position)!.X!.Keys.Select(k => (MathF.Round(k.T, 4), k.V)));

        // hop: y는 hop만 키를 놓는다 — 그 블록 앞(roll이 도는 동안에도)은 가만히 선 값, 1.5~1.7초에 뛰고, 마지막 값(0)이 남는다. 제 채널(Ground)은 그대로 돈다.
        Assert.Equal(0f, PosedAt(editor, "car.Body", 0.7).Y, 1);
        Assert.Equal(30f, PosedAt(editor, "car.Body", 1.6).Y, 1);
        Assert.Equal(0f, PosedAt(editor, "car.Body", 1.9).Y, 1);
        Assert.Equal(-100f, PosedAt(editor, "Ground", 1.0).Y, 1);

        // 품은 애니메이션의 이벤트가 그 시각에 울린다(1.5 + 0.1).
        EventKeyAsset thud = Assert.Single(EventKeys.Between(timeline, 0, 2));
        Assert.Equal(("thud", 1.6f), (thud.Name, MathF.Round(thud.T, 3)));

        // 구운 것도 문제가 없다.
        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));

        // 참조다 — 품은 애니메이션을 고치면 구운 것이 따라온다.
        editor.SetKeys("roll", "Body", ChannelKind.Position, Axis.X, new[] { Lin(0f, 0f), Lin(0.4f, 10f) });
        Assert.Equal(10f, PosedAt(editor, "car.Body", 1.4).X, 1);

        // 되돌리면 구운 것도 돌아간다. 블록을 다 지우면 구운 것이 없고 가만히 선 자리다.
        editor.Undo();
        Assert.Equal(100f, PosedAt(editor, "car.Body", 1.4).X, 1);
        editor.RemovePlayBlock("city_in", 1);
        editor.RemovePlayBlock("city_in", 0);
        Assert.Null(editor.Document.Find("city_in")!.Flattened);
        Assert.Equal(0f, PosedAt(editor, "car.Body", 1.4).X, 1);
    }

    [Fact]
    public void 재생_블록_앞은_가만히_선_값이고_다음_블록까지_마지막_값이_남으며_바깥의_제_키가_이기고_그림_키도_따라온다()
    {
        DocumentEditor editor = PlayEditor();
        // 바퀴의 가만히 선 회전을 20도로 — spin(0 → 90)을 1초와 1.5초에 한 번씩 튼다(사이에 틈이 없다).
        editor.UpdateObject("car", "Wheel", new ObjectAsset { X = 30f, Y = -40f, Width = 20f, Height = 20f, Rotation = 20f, Color = "#222222", Alpha = true });
        editor.SetDuration("spin", 0.4f);
        editor.SetKeys("spin", "Wheel", ChannelKind.Euler, Axis.Z, new[] { Lin(0f, 0f), Lin(0.4f, 90f) });
        editor.AddImageKey("spin", "Shell", 0.1f, "art/car_spin.png");
        editor.AddPlayBlock("city_in", "car", "spin", 0.5f);
        editor.AddPlayBlock("city_in", "car", "spin", 1.5f);
        TimelineAsset timeline = editor.Document.Find("city_in")!;
        ChannelAsset baked = timeline.Flattened!.FindChannel("car.Wheel", ChannelKind.Euler)!;
        float At(float t) => (float)Mt.Authoring.Graph.CurveSampler.Sample(baked.Z!.Keys, t, t + 0.0001, 1)[0].V;

        Assert.Equal(20f, At(0f), 1);     // 첫 블록 앞: 가만히 선 값(20도) — 키 값(0)이 아니다
        Assert.Equal(20f, At(0.49f), 1);
        Assert.Equal(0f, At(0.5f), 1);    // 블록이 시작하면 그 키의 값
        Assert.Equal(45f, At(0.7f), 1);   // 키 값 그대로(가만히 선 값에 더하지 않는다)
        Assert.Equal(90f, At(0.9f), 1);
        Assert.Equal(90f, At(1.2f), 1);   // 블록이 끝난 뒤 — 다음 블록까지 마지막 값이 남는다(20도로 돌아가지 않는다)
        Assert.Equal(90f, At(1.49f), 1);
        Assert.Equal(0f, At(1.5f), 1);    // 다음 블록 — 다시 처음부터
        Assert.Equal(90f, At(2f), 1);

        // 미리보기의 포즈도 같은 값이다.
        BuiltSets set = Fixtures.SetOf(editor.Document);
        float Posed(double t) => TimelinePose.Pose(set.BuildTree("city", "c/"), timeline, t, null, "c/").GetState("c/car.Wheel").LocalEulerAngles.Z;
        Assert.Equal((20f, 45f, 90f), (MathF.Round(Posed(0.2), 1), MathF.Round(Posed(0.7), 1), MathF.Round(Posed(1.2), 1)));

        // 품은 애니메이션의 그림 키도 경로 이름으로, 블록마다 그 시각에.
        Assert.Equal(new[] { ("car.Shell", 0.6f, "art/car_spin.png"), ("car.Shell", 1.6f, "art/car_spin.png") }, timeline.Flattened.ImageKeys.Select(w => (w.Object, MathF.Round(w.T, 3), w.Image)));
        Assert.Null(ImageKeys.ImageAt(timeline, "car.Shell", 0.5));
        Assert.Equal("art/car_spin.png", ImageKeys.ImageAt(timeline, "car.Shell", 0.7));

        // 바깥이 같은 경로에 제 키를 놓으면 그 성분은 제 키가 이긴다(여기서만 덮어쓴다).
        editor.AddChannel("city_in", "car.Wheel", ChannelKind.Euler);
        editor.SetKeys("city_in", "car.Wheel", ChannelKind.Euler, Axis.Z, new[] { Lin(0f, 0f), Lin(2f, 5f) });
        Assert.Equal(new[] { (0f, 0f), (2f, 5f) }, editor.Document.Find("city_in")!.Flattened!.FindChannel("car.Wheel", ChannelKind.Euler)!.Z!.Keys.Select(k => (k.T, k.V)));
        Assert.Equal(2.5f, MathF.Round(TimelinePose.Pose(set.BuildTree("city", "c/"), editor.Document.Find("city_in")!, 1.0, null, "c/").GetState("c/car.Wheel").LocalEulerAngles.Z, 1));
    }

    [Fact]
    public void 재생_블록은_품은_세트의_것만_길이_안에서_겹치지_않게_놓인다()
    {
        DocumentEditor editor = PlayEditor();
        editor.AddPlayBlock("city_in", "car", "roll", 0.5f);
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddPlayBlock("city_in", "Ground", "roll", 0f));       // 품은 오브젝트가 아니다
        Assert.Throws<InvalidOperationException>(() => editor.AddPlayBlock("city_in", "car", "city_in", 0f));       // 그 세트의 애니메이션이 아니다
        Assert.Throws<InvalidOperationException>(() => editor.AddPlayBlock("city_in", "car", "roll", 1.8f));        // 길이를 넘는다
        Assert.Throws<InvalidOperationException>(() => editor.AddPlayBlock("city_in", "car", "hop", 0.6f));         // 같은 인스턴스의 블록과 겹친다
        Assert.Throws<InvalidOperationException>(() => editor.AddPlayBlock("city_in", "car", "roll", 1f, repeat: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.RemovePlayBlock("city_in", 5));
        Assert.Equal(revision, editor.Revision);

        // 고치기 — 같은 값은 무기록, 시각이 바뀌면 자리가 옮겨진다.
        editor.AddPlayBlock("city_in", "car", "hop", 1.5f);
        revision = editor.Revision;
        Assert.Equal(0, editor.UpdatePlayBlock("city_in", 0, "car", "roll", 0.5f, 1));
        Assert.Equal(revision, editor.Revision);
        Assert.Equal(0, editor.UpdatePlayBlock("city_in", 1, "car", "hop", 0.1f, 1)); // roll 앞으로
        Assert.Equal(new[] { "hop", "roll" }, editor.Document.Find("city_in")!.Plays.Select(p => p.Timeline));
    }

    [Fact]
    public void 품은_애니메이션이_길어져_재생_블록이_넘치면_검증이_알리고_뒤쪽이_잘린_채_나간다()
    {
        DocumentEditor editor = PlayEditor();
        editor.AddPlayBlock("city_in", "car", "roll", 1.5f); // 1.5 + 0.4 ≤ 2
        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));

        // 품은 애니메이션을 길게(1초) — 바깥에서 막을 길이 없으니 검증이 알린다(막지는 않는다).
        editor.SetDuration("roll", 1f);
        editor.SetKeys("roll", "Body", ChannelKind.Position, Axis.X, new[] { Lin(0f, 0f), Lin(1f, 100f) });
        ValidationProblem problem = Assert.Single(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));
        Assert.False(problem.Blocking);
        Assert.Equal("city_in", problem.TimelineName);
        Assert.Contains("길이", problem.Message, StringComparison.Ordinal);

        // 구운 것은 길이 안에서 끝난다 — 시각은 오름차순 그대로다.
        ChannelAsset baked = editor.Document.Find("city_in")!.Flattened!.FindChannel("car.Body", ChannelKind.Position)!;
        Assert.Equal(2f, baked.X!.Keys[^1].T);
        Assert.All(baked.X.Keys.Zip(baked.X.Keys.Skip(1)), pair => Assert.True(pair.Second.T > pair.First.T));
    }

    [Fact]
    public void 재생_블록을_따로_떼면_구운_키가_제_키로_굳고_재생은_그대로이며_원본을_더는_안_따른다()
    {
        DocumentEditor editor = PlayEditor();
        editor.AddPlayBlock("city_in", "car", "roll", 0.5f, repeat: 2);
        editor.AddPlayBlock("city_in", "car", "hop", 1.5f);
        double[] times = { 0.3, 0.7, 1.1, 1.4, 1.6, 1.9 };
        List<Vec2> before = times.Select(t => PosedAt(editor, "car.Body", t)).ToList();

        // 그 인스턴스의 블록을 다 뗀다 — 한 칸. 블록이 사라지고 경로 이름의 채널이 제 것으로 선다. 이벤트도 옮겨 온다.
        long revision = editor.Revision;
        Assert.Equal(1, editor.DetachPlayBlocks("city_in", "car"));
        Assert.Equal(revision + 1, editor.Revision);
        TimelineAsset timeline = editor.Document.Find("city_in")!;
        Assert.Empty(timeline.Plays);
        Assert.Null(timeline.Flattened);
        Assert.NotNull(timeline.FindChannel("car.Body", ChannelKind.Position));
        Assert.Equal(("thud", 1.6f), (Assert.Single(timeline.Events).Name, MathF.Round(timeline.Events[0].T, 3)));

        // 재생은 그대로다.
        for (int i = 0; i < times.Length; i++)
        {
            Vec2 after = PosedAt(editor, "car.Body", times[i]);
            Assert.Equal(before[i].X, after.X, 1);
            Assert.Equal(before[i].Y, after.Y, 1);
        }

        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));

        // 원본을 고쳐도 더는 안 따라온다.
        editor.SetKeys("roll", "Body", ChannelKind.Position, Axis.X, new[] { Lin(0f, 0f), Lin(0.4f, 10f) });
        Assert.Equal(before[3].X, PosedAt(editor, "car.Body", 1.4).X, 1);

        // 블록이 없으면 막힌다. 되돌리면 블록이 돌아온다.
        Assert.Throws<InvalidOperationException>(() => editor.DetachPlayBlocks("city_in", "car"));
        editor.Undo();
        editor.Undo();
        Assert.Equal(2, editor.Document.Find("city_in")!.Plays.Count);
    }

    // ── 새 세트로 빼기 · 세트 이름 바꾸기 ─────────────────────────────

    [Fact]
    public void 고른_오브젝트들을_새_세트로_빼면_자리와_그림과_키를_데리고_가고_보이는_것은_그대로다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(new SetAsset { Name = "town", Objects = { new ObjectAsset { Id = "Ground", Width = 1920f, Height = 200f, Color = "#334455", Alpha = true } } });
        editor.AddObject("town", "Sun", null, image: true);
        editor.UpdateObject("town", "Sun", new ObjectAsset { X = 300f, Y = 200f, Width = 100f, Height = 100f, Image = true, Alpha = true });
        editor.AddObject("town", "Ray", "Sun");
        editor.UpdateObject("town", "Ray", new ObjectAsset { X = 10f, Width = 20f, Height = 20f, Color = "#FFFFFF", Alpha = true });
        editor.AddObject("town", "Glow", null, image: true);
        editor.UpdateObject("town", "Glow", new ObjectAsset { X = 310f, Y = 190f, Width = 300f, Height = 300f, Image = true, Alpha = true });
        editor.AddObject("town", "Tree", null, image: true);
        editor.AddSkin("town", "town");
        editor.SetSkinPart("town", "Sun", "art/sun.png");
        editor.SetSkinPart("town", "Glow", "art/glow.png");
        editor.SetSkinPart("town", "Tree", "art/tree.png");

        // 애니메이션 둘 — town_in(해가 y 100 → 200으로 떠오르고 빛살이 돈다, 나무가 민다) · slide(해가 자리 300 → 400). 키는 세트 안의 자리다.
        editor.AddTimeline("town_in", "town", 1f);
        editor.AddChannel("town_in", "Sun", ChannelKind.Position);
        editor.SetKeys("town_in", "Sun", ChannelKind.Position, Axis.Y, new[] { Lin(0f, 100f), Lin(1f, 200f) });
        editor.AddChannel("town_in", "Ray", ChannelKind.Euler);
        editor.SetKeys("town_in", "Ray", ChannelKind.Euler, Axis.Z, new[] { Lin(0f, 0f), Lin(1f, 90f) });
        editor.AddChannel("town_in", "Tree", ChannelKind.Position);
        editor.SetKeys("town_in", "Tree", ChannelKind.Position, Axis.X, new[] { Lin(0f, 50f), Lin(1f, 0f) });
        editor.AddTimeline("slide", "town", 1f);
        editor.AddChannel("slide", "Sun", ChannelKind.Position);
        editor.SetKeys("slide", "Sun", ChannelKind.Position, Axis.X, new[] { Lin(0f, 300f), Lin(1f, 400f) });
        editor.SetKeys("slide", "Sun", ChannelKind.Position, Axis.Y, new[] { Lin(0f, 200f), Lin(1f, 200f) });

        Vec2 World(string timeline, string node, double t)
        {
            BuiltSets set = Fixtures.SetOf(editor.Document);
            TimelineAsset asset = editor.Document.Find(timeline)!;
            return TimelinePose.Pose(set.BuildTree("town", "c/"), asset, t, null, "c/").TransformPoint("c/" + node, Vec3.Zero).XY;
        }

        double[] times = { 0, 0.3, 0.7, 1 };
        var before = times.Select(t => (World("town_in", "Sun", t), World("town_in", "Ray", t), World("town_in", "Glow", t), World("slide", "Sun", t))).ToList();

        // 해와 빛을 새 세트 star로 — 한 칸. 그 자리에 인스턴스가, 고른 것들의 가운데에 선다.
        long revision = editor.Revision;
        Assert.Equal("star", editor.ExtractObjects("town", new[] { "Sun", "Glow" }, "star"));
        Assert.Equal(revision + 1, editor.Revision);
        SetAsset town = editor.Document.FindSetOrCut("town")!;
        Assert.Equal(new[] { "Ground", "star", "Tree" }, town.Objects.Select(n => n.Id));
        Assert.Equal(("star", 310f, 190f), (town.Find("star")!.Set, town.Find("star")!.X, town.Find("star")!.Y));
        SetAsset star = editor.Document.FindSetOrCut("star")!;
        Assert.Equal(new[] { ("Sun", "", -10f, 10f), ("Ray", "Sun", 10f, 0f), ("Glow", "", 0f, 0f) }, star.Objects.Select(n => (n.Id, n.Parent, n.X, n.Y)));

        // 그림이 새 세트의 스킨으로, 키가 새 세트의 애니메이션으로 — 원래 애니메이션에는 그것을 트는 재생 블록이 선다.
        Assert.Equal(new[] { "Tree" }, editor.Document.FindSkin("town")!.Parts.Keys);
        Assert.Equal("star", editor.Document.FindSkin("star")!.Set);
        Assert.Equal(new[] { "Glow", "Sun" }, editor.Document.FindSkin("star")!.Parts.Keys);
        Assert.Equal(new[] { "Tree" }, editor.Document.Find("town_in")!.Channels.Select(c => c.Object));
        Assert.Equal(new[] { ("star", "star_in", 0f) }, editor.Document.Find("town_in")!.Plays.Select(p => (p.Object, p.Timeline, p.At)));
        Assert.Equal("star", editor.Document.Find("star_in")!.Set);
        Assert.Equal(new[] { "Sun", "Ray" }, editor.Document.Find("star_in")!.Channels.Select(c => c.Object));

        // 맨 위 오브젝트의 자리 키는 새 세트 안의 자리로 옮겨 적힌다(가운데 (310, 190)만큼) — 밑의 것(Ray)의 키는 그대로다.
        Assert.Equal(new[] { -90f, 10f }, editor.Document.Find("star_in")!.FindChannel("Sun", ChannelKind.Position)!.Y!.Keys.Select(k => k.V));
        Assert.Equal(new[] { 0f, 90f }, editor.Document.Find("star_in")!.FindChannel("Ray", ChannelKind.Euler)!.Z!.Keys.Select(k => k.V));
        ChannelAsset slid = editor.Document.Find("star_slide")!.FindChannel("Sun", ChannelKind.Position)!;
        Assert.Equal(new[] { -10f, 90f }, slid.X!.Keys.Select(k => k.V));
        Assert.Equal(new[] { 10f, 10f }, slid.Y!.Keys.Select(k => k.V));

        // 보이는 것은 그대로다 — 빼기 전후의 재생이 같다.
        var after = times.Select(t => (World("town_in", "star.Sun", t), World("town_in", "star.Ray", t), World("town_in", "star.Glow", t), World("slide", "star.Sun", t))).ToList();

        for (int i = 0; i < times.Length; i++)
        {
            Assert.Equal(before[i].Item1.X, after[i].Item1.X, 1);
            Assert.Equal(before[i].Item1.Y, after[i].Item1.Y, 1);
            Assert.Equal(before[i].Item2.X, after[i].Item2.X, 1);
            Assert.Equal(before[i].Item2.Y, after[i].Item2.Y, 1);
            Assert.Equal(before[i].Item3.X, after[i].Item3.X, 1);
            Assert.Equal(before[i].Item4.X, after[i].Item4.X, 1);
            Assert.Equal(before[i].Item4.Y, after[i].Item4.Y, 1);
        }

        BuiltSets composed = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, composed.NodeIds, composed.AlphaNodeIds));

        // 막는 것: 있는 이름 · 부모가 다른 것들. 문서는 그대로다.
        revision = editor.Revision;
        Assert.Throws<InvalidOperationException>(() => editor.ExtractObjects("town", new[] { "Tree" }, "star"));
        Assert.Throws<InvalidOperationException>(() => editor.ExtractObjects("star", new[] { "Ray", "Glow" }, "bits"));
        Assert.Equal(revision, editor.Revision);

        // 세트 이름 바꾸기 — 한 칸. 품은 곳 · 애니메이션 · 스킨이 따라오고, 세트의 이름을 딴 이름들(애니메이션 star_x · 스킨 star · 인스턴스 star)도 함께 바뀐다.
        editor.RenameSet("star", "sol");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal("sol", town.Find("sol")!.Set);
        Assert.Null(town.Find("star"));
        Assert.Equal(("sol", "sol"), (editor.Document.Find("sol_in")!.Set, editor.Document.FindSkin("sol")!.Set));
        Assert.NotNull(editor.Document.Find("sol_slide"));
        Assert.Equal(new[] { ("sol", "sol_in", 0f) }, editor.Document.Find("town_in")!.Plays.Select(p => (p.Object, p.Timeline, p.At)));
        Assert.Equal(before[2].Item1.Y, World("town_in", "sol.Sun", 0.7).Y, 1); // 재생은 그대로다
        Assert.Throws<InvalidOperationException>(() => editor.RenameSet("sol", "town"));

        // 되돌리면 통째로 돌아간다.
        editor.Undo();
        editor.Undo();
        Assert.Null(editor.Document.FindSetOrCut("star"));
        Assert.Equal(new[] { "Ground", "Sun", "Ray", "Glow", "Tree" }, editor.Document.FindSetOrCut("town")!.Objects.Select(n => n.Id));
        Assert.Empty(editor.Document.Find("town_in")!.Plays);
    }

    // ── 화면 밖에서 들어온다 · 떨어진 오브젝트를 함께 뺀다 ───────────────

    [Fact]
    public void 화면_밖에서_들어오는_거리는_오브젝트의_사각형이_무대_밖으로_다_나가는_만큼이다()
    {
        var stage = new Vec2(1920f, 1080f);
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(new SetAsset { Name = "town", Objects = { new ObjectAsset { Id = "Group", X = 100f, Y = -50f, Alpha = true } } });
        editor.AddObject("town", "Sun", null, image: true);
        editor.UpdateObject("town", "Sun", new ObjectAsset { X = 300f, Y = 200f, Width = 100f, Height = 100f, Image = true, Alpha = true });
        editor.AddObject("town", "Leaf", "Group", image: true);
        editor.UpdateObject("town", "Leaf", new ObjectAsset { X = 10f, Y = 20f, Width = 60f, Height = 80f, Rotation = 30f, Image = true, Alpha = true });
        editor.AddTimeline("town_in", "town", 1f);

        // 왼쪽에서: 무대의 반(960) + 자리(300) + 너비의 반(50) = 1310만큼 왼쪽에서 출발한다. 0.2초부터 0.5초 동안 — 앞뒤로는 가만히. 한 칸.
        // 키는 그 프레임의 자리다: 가만히 선 자리(300)에서 그만큼 떨어진 곳(−1010)에서 제자리(300)로.
        long revision = editor.Revision;
        Assert.Equal(-1310f, editor.SlideFromOutside("town_in", "Sun", StageSide.Left, 0.2f, 0.5f, stage));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { (0f, -1010f), (0.2f, -1010f), (0.7f, 300f), (1f, 300f) }, editor.Document.Find("town_in")!.FindChannel("Sun", ChannelKind.Position)!.X!.Keys.Select(k => (k.T, k.V)));

        // 위에서: 540 − 200 + 50 = 390. 나가는 것은 거꾸로(제자리 200 → 밖 590), 끝까지면 끝 키 하나로.
        Assert.Equal(390f, editor.SlideFromOutside("town_in", "Sun", StageSide.Top, 0.6f, 0.5f, stage, leaving: true));
        Assert.Equal(new[] { (0f, 200f), (0.6f, 200f), (1f, 590f) }, editor.Document.Find("town_in")!.FindChannel("Sun", ChannelKind.Position)!.Y!.Keys.Select(k => (k.T, k.V)));

        // 부모의 자리를 더하고, 돈 오브젝트는 감싸는 원으로 넉넉히: 960 − (100 + 10) + √(30² + 40²) = 900.
        Assert.Equal(900f, editor.SlideFromOutside("town_in", "Leaf", StageSide.Right, 0f, 0.5f, stage));
        Assert.Equal(new[] { 910f, 10f, 10f }, editor.Document.Find("town_in")!.FindChannel("Leaf", ChannelKind.Position)!.X!.Keys.Select(k => k.V)); // 부모 안의 자리(10)에서

        // 처음부터 끝까지면 키 둘이다.
        editor.AddTimeline("whole_in", "town", 1f);
        editor.SlideFromOutside("whole_in", "Sun", StageSide.Left, 0f, 1f, stage);
        Assert.Equal(new[] { -1010f, 300f }, editor.Document.Find("whole_in")!.FindChannel("Sun", ChannelKind.Position)!.X!.Keys.Select(k => k.V));

        // 길이 밖의 시작 · 없는 오브젝트는 막힌다.
        revision = editor.Revision;
        Assert.Throws<InvalidOperationException>(() => editor.SlideFromOutside("town_in", "Sun", StageSide.Left, 1f, 0.5f, stage));
        Assert.Throws<KeyNotFoundException>(() => editor.SlideFromOutside("town_in", "Nope", StageSide.Left, 0f, 0.5f, stage));
        Assert.Equal(revision, editor.Revision);
        BuiltSets set = Fixtures.SetOf(editor.Document);
        Assert.Empty(DocumentValidator.Validate(editor.Document, set.NodeIds, set.AlphaNodeIds));
    }

    [Fact]
    public void 떨어져_있는_오브젝트도_같은_부모_밑이면_한_세트로_빼진다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(new SetAsset { Name = "town" });

        foreach (string id in new[] { "A", "B", "C", "D" })
        {
            editor.AddObject("town", id, null, image: true);
        }

        // A와 C(사이에 B) — 첫 것의 자리에 인스턴스가 서고, 새 세트 안의 순서는 원래 순서다.
        Assert.Equal("pair", editor.ExtractObjects("town", new[] { "C", "A" }, "pair"));
        Assert.Equal(new[] { "pair", "B", "D" }, editor.Document.FindSetOrCut("town")!.Objects.Select(n => n.Id));
        Assert.Equal(new[] { "A", "C" }, editor.Document.FindSetOrCut("pair")!.Objects.Select(n => n.Id));
    }
}
