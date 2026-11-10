using System.Text.Json.Nodes;
using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;
using Mt.Cli;

namespace Mt.Authoring.Tests;

/// <summary>툴에서 정의한 리그(<c>PLAN-parts.md</c> §9) — 덤프를 기다리지 않고 이모지 리그를 만든다. Spine처럼 툴이 원본.</summary>
public class RigAssetTests
{
    private static readonly RigSchemaFile Dump = RigSchemaFile.Load(Fixtures.PathOf("rig-schemas.json")); // character · screenEffect뿐

    private static MotionEditor NewEditor()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(RigTemplates.Emoji());
        editor.AddSceneObject(null, "e1", RigTemplates.EmojiKind);
        editor.AttachObject(null, "e1", "s1");
        return editor;
    }

    [Fact]
    public void 문서의_리그는_덤프와_한_벌로_서고_조각과_불투명도_노드가_표시대로_나온다()
    {
        MotionEditor editor = NewEditor();
        Assert.DoesNotContain(RigTemplates.EmojiKind, Dump.RigKinds);
        RigSchemaFile rig = RigAssets.Compose(Dump, editor.Document);
        Assert.Contains(RigTemplates.EmojiKind, rig.RigKinds);
        Assert.Contains("character", rig.RigKinds);
        Assert.Equal(Dump.Sha256, rig.Sha256); // 문서가 기억하는 것은 덤프의 해시다
        Assert.Same(Dump, RigAssets.Compose(Dump, new MotionDocument())); // 더할 것이 없으면 그대로

        Assert.Equal(new[] { "Emoji_Part1", "Emoji_Part2", "Emoji_Part3", "Emoji_Part4" }, RigParts.Of(rig, RigTemplates.EmojiKind));
        Assert.Equal(new[] { "Emoji_Body", "Emoji_Part1", "Emoji_Part2", "Emoji_Part3", "Emoji_Part4" }, rig.AlphaNodeIds[RigTemplates.EmojiKind].OrderBy(n => n, StringComparer.Ordinal));
        Assert.Contains("Emoji_Anchor", rig.NodeIds[RigTemplates.EmojiKind]);

        // 씬에 서고(캐릭터의 슬롯 끝에 붙어), 조각은 정의한 크기다.
        RectNodeTree tree = ScenePreview.Build(rig, editor.Document.ActiveSceneAsset!);
        Assert.Equal("s1/CharSlot_Scale", tree.GetParentKey("e1/__root"));
        Assert.Equal("e1/Emoji_Body", tree.GetParentKey("e1/Emoji_Part2"));
        Assert.Equal(RigTemplates.DefaultPartSize, tree.GetRectSize("e1/Emoji_Part1").X, 3);

        // 조각은 이름이 아니라 표시(Image)로 가린다 — 이름 규칙에 안 맞아도 된다.
        editor.AddRig(new RigAsset { Kind = "prop", Nodes = { new RigNodeAsset { Id = "Hand" }, new RigNodeAsset { Id = "Leaf", Parent = "Hand", Width = 80, Height = 40, Image = true } } });
        RigSchemaFile both = RigAssets.Compose(Dump, editor.Document);
        Assert.Equal(new[] { "Leaf" }, RigParts.Of(both, "prop"));
        Assert.Empty(both.AlphaNodeIds["prop"]);

        // 모션·스킨·이모지 검증이 이 리그의 노드로 선다.
        editor.AddMotion("pop", RigTemplates.EmojiKind, 0.2f);
        editor.AddChannel("pop", "Emoji_Body", MotionChannelKind.Alpha);
        editor.SetKeys("pop", "Emoji_Body", MotionChannelKind.Alpha, Axis.X, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.2f, 1f) });
        Assert.Empty(MotionValidator.Validate(editor.Document, both.NodeIds, null, both.AlphaNodeIds));
        Assert.NotEmpty(MotionValidator.Validate(editor.Document, Dump.NodeIds)); // 덤프만으로는 모르는 리그다
    }

    [Fact]
    public void 같은_이름이면_문서의_리그가_덤프의_것을_이긴다()
    {
        // 런타임이 나중에 이모지 리그를 덤프에 넣어도, 문서가 정의한 것이 쓰인다(툴이 원본).
        RigSchemaFile dumpWithEmoji = RigSchemaFile.Load(EmojiRigFixture.WriteDump(Fixtures.PathOf("rig-schemas.json")));
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(RigTemplates.Emoji(parts: 2, partSize: 64f));
        RigSchemaFile rig = RigAssets.Compose(dumpWithEmoji, editor.Document);
        Assert.Equal(new[] { "Emoji_Part1", "Emoji_Part2" }, RigParts.Of(rig, RigTemplates.EmojiKind));
        Assert.Single(rig.Dto.rigs, r => r.rigKind == RigTemplates.EmojiKind);
    }

    [Fact]
    public void 편집기는_세울_수_있는_리그만_받고_조각을_다시_세운다()
    {
        MotionEditor editor = NewEditor();
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddRig(RigTemplates.Emoji()));                       // 이미 있다
        Assert.Throws<InvalidOperationException>(() => editor.AddRig(RigTemplates.Emoji("character")));            // 덤프가 원본인 리그
        Assert.Throws<InvalidOperationException>(() => editor.AddRig(RigTemplates.Emoji("camera")));
        Assert.Throws<InvalidOperationException>(() => editor.AddRig(RigTemplates.Emoji("1bad")));
        Assert.Throws<InvalidOperationException>(() => editor.AddRig(new RigAsset { Kind = "x", Nodes = { new RigNodeAsset { Id = "A", Parent = "B" }, new RigNodeAsset { Id = "B" } } })); // 부모가 뒤에
        Assert.Throws<InvalidOperationException>(() => editor.AddRig(new RigAsset { Kind = "x", Nodes = { new RigNodeAsset { Id = "A" }, new RigNodeAsset { Id = "A" } } }));
        Assert.Throws<InvalidOperationException>(() => editor.AddRig(new RigAsset { Kind = "x", Nodes = { new RigNodeAsset { Id = "a/b" } } }));
        Assert.Throws<InvalidOperationException>(() => editor.SetRigParts(RigTemplates.EmojiKind, 0, 100f));
        Assert.Throws<InvalidOperationException>(() => editor.SetRigParts(RigTemplates.EmojiKind, 3, 0f));
        Assert.Throws<KeyNotFoundException>(() => editor.SetRigParts("character", 3, 100f)); // 덤프의 리그는 못 고친다
        Assert.Equal(revision, editor.Revision);

        // 조각 여섯, 크기 96 — 한 칸. 다른 노드는 그대로다.
        editor.SetRigParts(RigTemplates.EmojiKind, 6, 96f);
        Assert.Equal(revision + 1, editor.Revision);
        RigAsset rig = editor.Document.FindRig(RigTemplates.EmojiKind)!;
        Assert.Equal(new[] { "Emoji_Anchor", "Emoji_Body", "Emoji_Part1", "Emoji_Part2", "Emoji_Part3", "Emoji_Part4", "Emoji_Part5", "Emoji_Part6" }, rig.Nodes.Select(n => n.Id));
        Assert.All(rig.Nodes.Where(n => n.Image), n => Assert.Equal((96f, "Emoji_Body", true), (n.Width, n.Parent, n.Alpha)));
        editor.Undo();
        Assert.Equal(4, editor.Document.FindRig(RigTemplates.EmojiKind)!.Nodes.Count(n => n.Image));

        // 받은 객체를 문서에 꽂지 않는다 — 밖에서 고쳐도 문서는 그대로.
        RigAsset given = RigTemplates.Emoji("prop");
        editor.AddRig(given);
        given.Nodes.Clear();
        Assert.Equal(6, editor.Document.FindRig("prop")!.Nodes.Count);

        // 저작본 왕복. 리그가 없는 문서에는 안 적힌다.
        string json = MotionDocumentJson.Write(editor.Document);
        Assert.Equal(json, MotionDocumentJson.Write(MotionDocumentJson.Read(json)));
        Assert.DoesNotContain("\"rigs\"", MotionDocumentJson.Write(new MotionDocument()), StringComparison.Ordinal);

        // 지우면 그 리그의 오브젝트는 남고(검증이 알린다) 씬은 던지지 않고 선다.
        editor.RemoveRig(RigTemplates.EmojiKind);
        var missing = new List<SceneObject>();
        ScenePreview.Build(RigAssets.Compose(Dump, editor.Document), editor.Document.ActiveSceneAsset!, null, missing);
        Assert.Equal("e1", Assert.Single(missing).Id);
    }

    [Fact]
    public void 내보낸_리그는_덤프와_같은_모양이라_같은_로더로_읽힌다()
    {
        MotionEditor editor = NewEditor();
        string json = RigAssets.WriteSchemas(editor.Document, new Vec2(1920f, 1080f));
        Assert.Equal(json, RigAssets.WriteSchemas(editor.Document, new Vec2(1920f, 1080f))); // 결정적

        RigSchemaFile read = RigSchemaFile.Parse(System.Text.Encoding.UTF8.GetBytes(json)); // 캐릭터 덤프를 읽는 그 길
        Assert.Equal(new[] { RigTemplates.EmojiKind }, read.RigKinds);
        Assert.Equal(4, RigParts.Of(read, RigTemplates.EmojiKind).Count);
        Assert.Equal(5, read.AlphaNodeIds[RigTemplates.EmojiKind].Count);
        Assert.Equal(RigTemplates.DefaultPartSize, read.BuildTree(RigTemplates.EmojiKind).GetRectSize("Emoji_Part1").X, 3);

        JsonNode root = JsonNode.Parse(json)!;
        JsonNode part = root["rigs"]![0]!["nodes"]!.AsArray().First(n => n!["id"]!.GetValue<string>() == "Emoji_Part1")!;
        Assert.True(part["hasImage"]!.GetValue<bool>());
        Assert.True(part["hasCanvasGroup"]!.GetValue<bool>());
        Assert.Equal("__root", root["rigs"]![0]!["nodes"]![0]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void 명령줄도_문서의_리그로_검증하고_내보낸다()
    {
        MotionEditor editor = NewEditor();
        editor.AddMotion("pop", RigTemplates.EmojiKind, 0.2f, MotionEnd.Commit, MotionChannelMode.Absolute);
        editor.AddChannel("pop", "Emoji_Body", MotionChannelKind.Position);
        editor.SetKeys("pop", "Emoji_Body", MotionChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.2f, 40f) });

        string directory = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", $"rig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string document = Path.Combine(directory, "a.motion-authoring.json");
        File.WriteAllText(document, MotionDocumentJson.Write(editor.Document));

        int Run(out JsonNode? json, params string[] args)
        {
            var stdout = new StringWriter();
            int code = MtCli.Run(args, stdout, new StringWriter());
            json = JsonNode.Parse(stdout.ToString());
            return code;
        }

        Assert.Equal(0, Run(out JsonNode? validated, "validate", document)); // 내장 덤프에는 emoji가 없다 — 문서의 리그로 통과한다
        Assert.Empty(validated!["problems"]!.AsArray());
        Assert.Equal(0, Run(out JsonNode? pose, "pose", document, "--motion", "pop", "--time", "0.2"));
        Assert.Equal(40.0, pose!["nodes"]!["Emoji_Body"]!["position"]![1]!.GetValue<double>(), 3);

        string target = Path.Combine(directory, "rigs.json");
        Assert.Equal(0, Run(out JsonNode? exported, "rigs", document, target));
        Assert.Equal(4, exported!["rigs"]![0]!["parts"]!.GetValue<int>());
        Assert.Contains(RigTemplates.EmojiKind, RigSchemaFile.Load(target).RigKinds);
    }

    // ── 리그 편집기(PLAN-parts.md §12) ─────────────────────────────────

    private static string[] Ids(MotionEditor editor, string kind = RigTemplates.EmojiKind) => editor.Document.FindRig(kind)!.Nodes.Select(n => n.Id).ToArray();

    [Fact]
    public void 노드를_짓고_지우고_옮기면_부모가_늘_자식보다_먼저_온다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(RigTemplates.Emoji(parts: 2));
        long revision = editor.Revision;

        // 더하기 — 부모의 식구들 맨 뒤. 그림 노드는 본의 크기와 불투명도를 갖고 선다.
        RigNodeAsset glow = editor.AddRigNode(RigTemplates.EmojiKind, "Emoji_Glow", "Emoji_Anchor", image: true);
        Assert.Equal((160f, 160f, true, true), (glow.Width, glow.Height, glow.Image, glow.Alpha));
        editor.AddRigNode(RigTemplates.EmojiKind, "Emoji_Spark", "Emoji_Glow");
        editor.AddRigNode(RigTemplates.EmojiKind, "Extra"); // 루트 밑
        Assert.Equal(revision + 3, editor.Revision);
        Assert.Equal(new[] { "Emoji_Anchor", "Emoji_Body", "Emoji_Part1", "Emoji_Part2", "Emoji_Glow", "Emoji_Spark", "Extra" }, Ids(editor));

        // 막는 것 — 기록이 안 남는다.
        revision = editor.Revision;
        Assert.Throws<InvalidOperationException>(() => editor.AddRigNode(RigTemplates.EmojiKind, "Emoji_Glow"));       // 이미 있다
        Assert.Throws<InvalidOperationException>(() => editor.AddRigNode(RigTemplates.EmojiKind, "1bad"));
        Assert.Throws<InvalidOperationException>(() => editor.AddRigNode(RigTemplates.EmojiKind, "a/b"));
        Assert.Throws<KeyNotFoundException>(() => editor.AddRigNode(RigTemplates.EmojiKind, "X", "Nope"));
        Assert.Throws<KeyNotFoundException>(() => editor.AddRigNode("character", "X"));                                 // 덤프의 리그는 못 고친다
        Assert.Throws<InvalidOperationException>(() => editor.SetRigNodeParent(RigTemplates.EmojiKind, "Emoji_Glow", "Emoji_Spark")); // 제 밑으로
        Assert.Throws<InvalidOperationException>(() => editor.UpdateRigNode(RigTemplates.EmojiKind, "Extra", new RigNodeAsset { Width = -1f }));
        Assert.Equal(revision, editor.Revision);

        // 형제 사이의 순서 — 식구째 옮긴다. 끝에서는 아무 일도 없다.
        editor.MoveRigNode(RigTemplates.EmojiKind, "Emoji_Glow", -1);
        Assert.Equal(new[] { "Emoji_Anchor", "Emoji_Glow", "Emoji_Spark", "Emoji_Body", "Emoji_Part1", "Emoji_Part2", "Extra" }, Ids(editor));
        editor.MoveRigNode(RigTemplates.EmojiKind, "Emoji_Glow", -1);
        editor.MoveRigNode(RigTemplates.EmojiKind, "Emoji_Part2", +1);
        Assert.Equal(revision + 1, editor.Revision);
        editor.MoveRigNode(RigTemplates.EmojiKind, "Emoji_Anchor", +1);
        Assert.Equal(new[] { "Extra", "Emoji_Anchor", "Emoji_Glow", "Emoji_Spark", "Emoji_Body", "Emoji_Part1", "Emoji_Part2" }, Ids(editor));

        // 부모 바꾸기 — 식구째, 새 부모의 맨 뒤로. 같은 부모면 무기록.
        revision = editor.Revision;
        editor.SetRigNodeParent(RigTemplates.EmojiKind, "Emoji_Glow", "Emoji_Body");
        editor.SetRigNodeParent(RigTemplates.EmojiKind, "Emoji_Glow", "Emoji_Body");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { "Extra", "Emoji_Anchor", "Emoji_Body", "Emoji_Part1", "Emoji_Part2", "Emoji_Glow", "Emoji_Spark" }, Ids(editor));
        Assert.Equal("Emoji_Body", editor.Document.FindRig(RigTemplates.EmojiKind)!.Find("Emoji_Glow")!.Parent);
        editor.SetRigNodeParent(RigTemplates.EmojiKind, "Emoji_Body", null); // 루트로
        Assert.Equal(new[] { "Extra", "Emoji_Anchor", "Emoji_Body", "Emoji_Part1", "Emoji_Part2", "Emoji_Glow", "Emoji_Spark" }, Ids(editor));
        Assert.Equal("", editor.Document.FindRig(RigTemplates.EmojiKind)!.Find("Emoji_Body")!.Parent);

        // 값 — 한 칸, 같은 값은 무기록. 세운 나무에 그대로 선다.
        revision = editor.Revision;
        var moved = new RigNodeAsset { X = 30f, Y = -12f, Width = 80f, Height = 40f, Image = true, Alpha = false };
        editor.UpdateRigNode(RigTemplates.EmojiKind, "Emoji_Glow", moved);
        editor.UpdateRigNode(RigTemplates.EmojiKind, "Emoji_Glow", moved);
        Assert.Equal(revision + 1, editor.Revision);
        RigSchemaFile composed = RigAssets.Compose(Dump, editor.Document);
        Assert.True(composed.TryGetRig(RigTemplates.EmojiKind, out RigSchemaRigDto dto));
        RigSchemaNodeDto node = dto.nodes.Single(n => n.id == "Emoji_Glow");
        Assert.Equal((30f, -12f, 80f, 40f, true, false), (node.anchoredPosition.x, node.anchoredPosition.y, node.sizeDelta.x, node.sizeDelta.y, node.hasImage, node.hasCanvasGroup));
        Assert.Contains("Emoji_Glow", RigParts.Of(composed, RigTemplates.EmojiKind));

        // 가만히 선 자세의 회전·배율(배치 모드) — 세운 나무에 그대로 선다. 배율 0은 막는다.
        editor.UpdateRigNode(RigTemplates.EmojiKind, "Emoji_Glow", new RigNodeAsset { X = 30f, Y = -12f, Width = 80f, Height = 40f, Image = true, Rotation = 30f, ScaleX = 2f, ScaleY = -1f });
        Assert.True(RigAssets.Compose(Dump, editor.Document).TryGetRig(RigTemplates.EmojiKind, out RigSchemaRigDto posed));
        RigSchemaNodeDto turned = posed.nodes.Single(n => n.id == "Emoji_Glow");
        Assert.Equal((30f, 2f, -1f), (turned.localEulerAngles.z, turned.localScale.x, turned.localScale.y));
        Assert.Throws<InvalidOperationException>(() => editor.UpdateRigNode(RigTemplates.EmojiKind, "Emoji_Glow", new RigNodeAsset { ScaleX = 0f }));
        editor.Undo();

        // 지우기 — 밑의 노드도 함께. 되돌리면 그 자리에 돌아온다.
        editor.RemoveRigNode(RigTemplates.EmojiKind, "Emoji_Body");
        Assert.Equal(new[] { "Extra", "Emoji_Anchor" }, Ids(editor));
        editor.Undo();
        Assert.Equal(7, Ids(editor).Length);
        RigAssets.Compose(Dump, editor.Document); // 여전히 세울 수 있다
    }

    [Fact]
    public void 노드의_이름을_바꾸면_채널과_스킨과_이모지와_붙는_노드가_따라온다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(RigTemplates.Emoji(parts: 2));
        editor.AddSceneObject(null, "e1", RigTemplates.EmojiKind);
        editor.AddSkin("땀", RigTemplates.EmojiKind);
        editor.SetSkinPart("땀", "Emoji_Part1", "art/sweat.png");
        editor.AddMotion("pop", RigTemplates.EmojiKind, 0.2f);
        editor.AddChannel("pop", "Emoji_Part1", MotionChannelKind.Position);
        editor.AddChannel("pop", "Emoji_Body", MotionChannelKind.Position);
        editor.AddEmoji("sweat", RigTemplates.EmojiKind);

        long revision = editor.Revision;
        editor.RenameRigNode(RigTemplates.EmojiKind, "Emoji_Part1", "Drop");
        editor.RenameRigNode(RigTemplates.EmojiKind, "Emoji_Anchor", "Pin");
        editor.RenameRigNode(RigTemplates.EmojiKind, "Pin", "Pin"); // 같은 이름은 무기록
        Assert.Equal(revision + 2, editor.Revision);

        Assert.Equal(new[] { "Pin", "Emoji_Body", "Drop", "Emoji_Part2" }, Ids(editor));
        Assert.Equal("Pin", editor.Document.FindRig(RigTemplates.EmojiKind)!.Find("Emoji_Body")!.Parent);
        Assert.Equal(new[] { "Drop", "Emoji_Body" }, editor.Document.Find("pop")!.Channels.Select(c => c.Node));
        Assert.Equal("art/sweat.png", editor.Document.FindSkin("땀")!.Parts["Drop"]);
        Assert.False(editor.Document.FindSkin("땀")!.Parts.ContainsKey("Emoji_Part1"));
        Assert.Equal("Pin", editor.Document.FindEmoji("sweat")!.AnchorNode);

        // 이름이 겹치거나 규칙에 안 맞으면 막는다. 되돌리면 전부 함께 돌아온다.
        Assert.Throws<InvalidOperationException>(() => editor.RenameRigNode(RigTemplates.EmojiKind, "Drop", "Emoji_Body"));
        Assert.Throws<InvalidOperationException>(() => editor.RenameRigNode(RigTemplates.EmojiKind, "Drop", "물방울"));
        editor.Undo();
        editor.Undo();
        Assert.Equal("Emoji_Part1", editor.Document.Find("pop")!.Channels[0].Node);
        Assert.Equal("Emoji_Anchor", editor.Document.FindEmoji("sweat")!.AnchorNode);
        Assert.True(editor.Document.FindSkin("땀")!.Parts.ContainsKey("Emoji_Part1"));
    }

    [Fact]
    public void 색_오브젝트는_덤프와_같은_모양에_fillColor로_나가고_색_글은_고른_꼴로_선다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(new RigAsset { Kind = "wipe", Nodes = { new RigNodeAsset { Id = "Body", Alpha = true } } });
        editor.AddRigNode("wipe", "Panel", "Body");
        long revision = editor.Revision;

        // 색을 주면 색 오브젝트 — 한 칸. "#" 없이, 소문자로 적어도 고른 꼴로 선다. 같은 값은 무기록.
        editor.UpdateRigNode("wipe", "Panel", new RigNodeAsset { Width = 1920f, Height = 400f, Color = "3d5a80", Alpha = true });
        editor.UpdateRigNode("wipe", "Panel", new RigNodeAsset { Width = 1920f, Height = 400f, Color = "#3D5A80", Alpha = true });
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal("#3D5A80", editor.Document.FindRig("wipe")!.Find("Panel")!.Color);
        Assert.Throws<InvalidOperationException>(() => editor.UpdateRigNode("wipe", "Panel", new RigNodeAsset { Color = "blue" }));
        Assert.Equal("#11223344", MotionEditor.NormalizeColor(" #11223344 ")); // 불투명도까지
        Assert.Null(MotionEditor.NormalizeColor("  "));

        // 세운 리그: 색 표에 그 노드가 서고, 그림 노드는 아니다(조각이 아니다).
        RigSchemaFile composed = RigAssets.Compose(Dump, editor.Document);
        Assert.Equal("#3D5A80", RigParts.ColorsOf(composed, "wipe")["Panel"]);
        Assert.Empty(RigParts.Of(composed, "wipe"));
        Assert.Empty(RigParts.ColorsOf(composed, "character"));

        // 산출물과 저작본 — 색이 없는 노드에는 안 적힌다(저작본).
        Assert.Contains("\"fillColor\": \"#3D5A80\"", RigAssets.WriteSchemas(editor.Document, new Vec2(1920f, 1080f)), StringComparison.Ordinal);
        string json = MotionDocumentJson.Write(editor.Document);
        Assert.Equal(1, json.Split("\"color\"").Length - 1);
        Assert.Equal("#3D5A80", MotionDocumentJson.Read(json).FindRig("wipe")!.Find("Panel")!.Color);

        // 비우면 색 오브젝트가 아니다.
        editor.UpdateRigNode("wipe", "Panel", new RigNodeAsset { Width = 1920f, Height = 400f, Alpha = true });
        Assert.Null(editor.Document.FindRig("wipe")!.Find("Panel")!.Color);
        Assert.Empty(RigParts.ColorsOf(RigAssets.Compose(Dump, editor.Document), "wipe"));
    }

    // ── 시차 — 오브젝트의 움직임을 통째로 민다 ─────────────────────────

    [Fact]
    public void 움직임을_밀면_길이는_그대로이고_비는_쪽은_가만히_넘치는_쪽은_잘린다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(RigTemplates.Emoji(parts: 3));
        editor.AddMotion("in", RigTemplates.EmojiKind, 1f, MotionEnd.Commit);
        KeyAsset Lin(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Linear };

        foreach (string part in new[] { "Emoji_Part1", "Emoji_Part2", "Emoji_Part3" })
        {
            editor.AddChannel("in", part, MotionChannelKind.Position);
            editor.SetKeys("in", part, MotionChannelKind.Position, Axis.X, new[] { Lin(0f, -100f), Lin(0.2f, -100f), Lin(0.6f, 0f), Lin(1f, 0f) }); // 0.2~0.6초에 들어온다
        }

        (float T, float V)[] Keys(string part) => editor.Document.Find("in")!.FindChannel(part, MotionChannelKind.Position)!.X!.Keys.Select(k => (MathF.Round(k.T, 3), MathF.Round(k.V, 1))).ToArray();
        long revision = editor.Revision;

        // 0.1초 늦게 — 안쪽 키가 밀리고 처음과 끝은 제 값으로 선다. 한 칸.
        Assert.True(editor.ShiftNodeKeys("in", "Emoji_Part1", 0.1f));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { (0f, -100f), (0.3f, -100f), (0.7f, 0f), (1f, 0f) }, Keys("Emoji_Part1"));

        // 넘치게 늦추면 잘린다 — 끝의 키는 잘린 자리의 값(아직 들어오는 중).
        editor.ShiftNodeKeys("in", "Emoji_Part2", 0.6f);
        Assert.Equal(new[] { (0f, -100f), (0.8f, -100f), (1f, -50f) }, Keys("Emoji_Part2"));

        // 일찍 — 처음의 키는 잘린 자리의 값(이미 들어오는 중), 끝은 마지막 값으로 가만히.
        editor.ShiftNodeKeys("in", "Emoji_Part3", -0.4f);
        Assert.Equal(new[] { (0f, -50f), (0.2f, 0f), (1f, 0f) }, Keys("Emoji_Part3"));

        // 어느 쪽이든 규칙(처음과 끝에 키)이 그대로 선다.
        RigSchemaFile rig = RigAssets.Compose(Dump, editor.Document);
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, null, rig.AlphaNodeIds));

        // 채널이 없는 오브젝트·0초는 아무 일도 없다(기록 없음).
        revision = editor.Revision;
        Assert.False(editor.ShiftNodeKeys("in", "Emoji_Body", 0.1f));
        Assert.False(editor.ShiftNodeKeys("in", "Emoji_Part1", 0f));
        Assert.Equal(revision, editor.Revision);

        // 시차 — 순서대로 0.1초씩 더: 첫째는 그대로. 통째로 한 칸.
        editor.Undo();
        editor.Undo();
        editor.Undo();
        revision = editor.Revision;
        Assert.True(editor.StaggerNodes("in", new[] { "Emoji_Part1", "Emoji_Part2", "Emoji_Part3" }, 0.1f));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(0.2f, Keys("Emoji_Part1")[1].T);
        Assert.Equal(0.3f, Keys("Emoji_Part2")[1].T);
        Assert.Equal(0.4f, Keys("Emoji_Part3")[1].T);
    }

    // ── 제 차례 전에는 숨긴다 ───────────────────────────────────────────

    [Fact]
    public void 제_차례_전까지_숨기면_움직이기_시작하는_시각에_나타나는_불투명도_키가_놓인다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(RigTemplates.Emoji(parts: 3));
        editor.AddMotion("in", RigTemplates.EmojiKind, 1f, MotionEnd.Commit);
        KeyAsset Lin(float t, float v) => new() { T = t, V = v, Tangent = TangentMode.Linear };
        editor.AddChannel("in", "Emoji_Part1", MotionChannelKind.Position); // 0.4초에 움직이기 시작해 0.8초에 선다
        editor.SetKeys("in", "Emoji_Part1", MotionChannelKind.Position, Axis.X, new[] { Lin(0f, -100f), Lin(0.4f, -100f), Lin(0.8f, 0f), Lin(1f, 0f) });
        editor.AddChannel("in", "Emoji_Part2", MotionChannelKind.Position); // 처음부터 끝까지 움직인다
        editor.SetKeys("in", "Emoji_Part2", MotionChannelKind.Position, Axis.X, new[] { Lin(0f, -100f), Lin(1f, 0f) });
        (float T, float V)[] Alpha(string part) => editor.Document.Find("in")!.FindChannel(part, MotionChannelKind.Alpha)!.X!.Keys.Select(k => (MathF.Round(k.T, 3), k.V)).ToArray();

        // 제 차례(0.4초) 전까지 0, 0.1초에 걸쳐 나타난다 — 한 칸.
        long revision = editor.Revision;
        Assert.True(editor.HideUntilTurn("in", "Emoji_Part1"));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(new[] { (0f, 0f), (0.4f, 0f), (0.5f, 1f), (1f, 1f) }, Alpha("Emoji_Part1"));

        // 움직임을 마치면(0.8초) 숨긴다 — 있던 불투명도 커브를 갈아 치운다.
        Assert.True(editor.HideAfterTurn("in", "Emoji_Part1"));
        Assert.Equal(new[] { (0f, 1f), (0.7f, 1f), (0.8f, 0f), (1f, 0f) }, Alpha("Emoji_Part1"));

        // 처음부터(끝까지) 움직이거나 움직임이 없는 오브젝트는 할 일이 없다(기록 없음).
        revision = editor.Revision;
        Assert.False(editor.HideUntilTurn("in", "Emoji_Part2"));
        Assert.False(editor.HideAfterTurn("in", "Emoji_Part2"));
        Assert.False(editor.HideUntilTurn("in", "Emoji_Part3"));
        Assert.Equal(revision, editor.Revision);

        // 전부 — 움직이는 오브젝트 가운데 늦게 나오는 것만, 한 칸. 규칙에 맞는다.
        editor.Undo();
        editor.Undo();
        revision = editor.Revision;
        Assert.Equal(1, editor.HideAllUntilTurn("in"));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Null(editor.Document.Find("in")!.FindChannel("Emoji_Part2", MotionChannelKind.Alpha));
        RigSchemaFile rig = RigAssets.Compose(Dump, editor.Document);
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, null, rig.AlphaNodeIds));
    }

    // ── 세트가 세트를 품는다(PLAN-set.md S9) ───────────────────────────

    /// <summary>car(Body › Shell(그림) · Wheel(색)) · sun(Disc(그림)) · city(Ground(색) + car 인스턴스 + sun 인스턴스).</summary>
    private static MotionEditor NestedEditor()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(new RigAsset { Kind = "car", Nodes = { new RigNodeAsset { Id = "Body", Alpha = true } } });
        editor.AddRigNode("car", "Shell", "Body", image: true);
        editor.AddRigNode("car", "Wheel", "Body");
        editor.UpdateRigNode("car", "Wheel", new RigNodeAsset { X = 30f, Y = -40f, Width = 20f, Height = 20f, Color = "#222222", Alpha = true });
        editor.AddSkin("car_red", "car");
        editor.SetSkinPart("car_red", "Shell", "art/car_red.png");
        editor.AddSkin("car_blue", "car");
        editor.SetSkinPart("car_blue", "Shell", "art/car_blue.png");

        editor.AddRig(new RigAsset { Kind = "sun", Nodes = { new RigNodeAsset { Id = "Disc", Width = 100f, Height = 100f, Image = true, Alpha = true } } });
        editor.AddSkin("sun", "sun");
        editor.SetSkinPart("sun", "Disc", "art/sun.png");

        editor.AddRig(new RigAsset { Kind = "city", Nodes = { new RigNodeAsset { Id = "Ground", Width = 1920f, Height = 200f, Color = "#334455", Alpha = true } } });
        return editor;
    }

    [Fact]
    public void 세트를_품으면_그_오브젝트들이_경로_이름으로_펼쳐져_한_덩어리로_선다()
    {
        MotionEditor editor = NestedEditor();
        long revision = editor.Revision;

        // 품는다 — 한 칸. 인스턴스 오브젝트는 통째로 옮기는 손잡이다(자리 · 배율).
        RigNodeAsset car = editor.AddRigInstance("city", "car", "car");
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(("car", true), (car.Set, car.Alpha));
        editor.UpdateRigNode("city", "car", new RigNodeAsset { X = 300f, Y = 50f, ScaleX = 2f, ScaleY = 2f, Alpha = true });
        Assert.Equal("car", editor.Document.FindRig("city")!.Find("car")!.Set); // 값을 고쳐도 품은 것은 그대로다
        editor.AddRigInstance("city", "sun", "sun");

        // 세운 리그: 품은 세트가 인스턴스 밑에 "인스턴스.이름"으로 펼쳐진다. 부모·색·그림 표시가 따라온다.
        RigSchemaFile composed = RigAssets.Compose(Dump, editor.Document);
        Assert.True(composed.TryGetRig("city", out RigSchemaRigDto dto));
        Assert.Equal(new[] { "__root", "Ground", "car", "car.Body", "car.Shell", "car.Wheel", "sun", "sun.Disc" }, dto.nodes.Select(n => n.id));
        Assert.Equal(("car", "car.Body", "car.Body"), (dto.nodes.Single(n => n.id == "car.Body").parent, dto.nodes.Single(n => n.id == "car.Shell").parent, dto.nodes.Single(n => n.id == "car.Wheel").parent));
        Assert.Equal(new[] { "car.Shell", "sun.Disc" }, RigParts.Of(composed, "city"));
        Assert.Equal("#222222", RigParts.ColorsOf(composed, "city")["car.Wheel"]);
        Assert.Equal("car", RigAssetCodec.OwnerOf("car.Wheel"));

        // 선 자리: 인스턴스의 자리·배율이 안쪽에 곱해진다 — 바퀴는 (300 + 30×2, 50 − 40×2).
        var scene = new SceneAsset { Name = "s", Objects = { new SceneObject { Id = "c", Rig = "city" } } };
        RectNodeTree tree = ScenePreview.Build(composed, scene);
        Vec2 root = tree.TransformPoint("c/__root", Vec3.Zero).XY;
        Vec2 wheel = tree.TransformPoint("c/car.Wheel", Vec3.Zero).XY;
        Assert.Equal((360f, -30f), (MathF.Round(wheel.X - root.X, 1), MathF.Round(wheel.Y - root.Y, 1)));

        // 실제로 입는 스킨: 품은 세트의 스킨이 경로 이름으로 — 인스턴스가 고른 것, 없으면 그 세트의 첫 스킨.
        SkinAsset worn = RigAssets.EffectiveSkin(editor.Document, "city", null)!;
        Assert.Equal(("art/car_red.png", "art/sun.png"), (worn.Parts["car.Shell"], worn.Parts["sun.Disc"]));
        editor.SetRigInstanceSkin("city", "car", "car_blue");
        Assert.Equal("art/car_blue.png", RigAssets.EffectiveSkin(editor.Document, "city", null)!.Parts["car.Shell"]);
        Assert.Throws<InvalidOperationException>(() => editor.SetRigInstanceSkin("city", "car", "sun"));     // 다른 세트의 스킨
        Assert.Throws<InvalidOperationException>(() => editor.SetRigInstanceSkin("city", "Ground", "sun"));  // 품은 오브젝트가 아니다
        Assert.Same(editor.Document.FindSkin("sun"), RigAssets.EffectiveSkin(editor.Document, "sun", "sun")); // 품은 것이 없으면 제 스킨 그대로

        // 참조다 — 원본 세트를 고치면 품은 곳이 따라온다.
        editor.AddRigNode("car", "Light", "Body");
        Assert.True(RigAssets.Compose(Dump, editor.Document).TryGetRig("city", out RigSchemaRigDto after));
        Assert.Contains(after.nodes, n => n.id == "car.Light");

        // 품은 것의 애니메이션: 인스턴스를 통째로 움직이는 키도, 안쪽의 것을 경로 이름으로 짚는 키도 놓인다 — 검증을 지난다.
        editor.AddMotion("city_in", "city", 1f, MotionEnd.Commit);
        editor.AddChannel("city_in", "car", MotionChannelKind.Position);
        editor.SetKeys("city_in", "car", MotionChannelKind.Position, Axis.X, new[] { new KeyAsset { T = 0f, V = -500f, Tangent = TangentMode.Linear }, new KeyAsset { T = 1f, V = 0f, Tangent = TangentMode.Linear } });
        editor.AddChannel("city_in", "car.Wheel", MotionChannelKind.Euler);
        editor.SetKeys("city_in", "car.Wheel", MotionChannelKind.Euler, Axis.Z, new[] { new KeyAsset { T = 0f, V = 0f, Tangent = TangentMode.Linear }, new KeyAsset { T = 1f, V = -360f, Tangent = TangentMode.Linear } });
        RigSchemaFile rig = RigAssets.Compose(Dump, editor.Document);
        Assert.Empty(MotionValidator.Validate(editor.Document, rig.NodeIds, null, rig.AlphaNodeIds));

        // 산출물: 펼친 한 덩어리로 나간다(런타임은 품는다는 것을 모른다). 저작본은 참조만 적는다.
        string schemas = RigAssets.WriteSchemas(editor.Document, new Vec2(1920f, 1080f));
        Assert.Contains("\"id\": \"car.Wheel\"", schemas, StringComparison.Ordinal);
        string json = MotionDocumentJson.Write(editor.Document);
        Assert.Contains("\"set\": \"car\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("car.Wheel\",\n          \"parent", json, StringComparison.Ordinal);
        Assert.Equal(json, MotionDocumentJson.Write(MotionDocumentJson.Read(json)));
    }

    [Fact]
    public void 서로_품는_것과_품긴_세트를_지우는_것은_막는다()
    {
        MotionEditor editor = NestedEditor();
        editor.AddRigInstance("city", "car", "car");
        long revision = editor.Revision;

        Assert.Throws<InvalidOperationException>(() => editor.AddRigInstance("city", "self", "city"));  // 자기 자신
        Assert.Throws<InvalidOperationException>(() => editor.AddRigInstance("car", "back", "city"));   // city가 car를 품고 있다
        editor.AddRigInstance("sun", "inner", "car");                                                     // sun → car는 된다
        Assert.Throws<InvalidOperationException>(() => editor.AddRigInstance("car", "far", "sun"));     // 두 다리 건너도 막는다
        Assert.Throws<KeyNotFoundException>(() => editor.AddRigInstance("city", "ghost", "nope"));
        Assert.Throws<InvalidOperationException>(() => editor.RemoveRig("car"));                          // 품긴 세트는 못 지운다
        Assert.Equal(revision + 1, editor.Revision);

        // 품은 것이 또 품은 것 — 경로가 이어진다.
        editor.AddRigInstance("city", "sun", "sun");
        Assert.True(RigAssets.Compose(Dump, editor.Document).TryGetRig("city", out RigSchemaRigDto dto));
        Assert.Contains(dto.nodes, n => n.id == "sun.inner.Wheel");

        // 인스턴스를 빼면 지울 수 있다.
        editor.RemoveRigNode("city", "car");
        editor.RemoveRigNode("sun", "inner");
        editor.RemoveRig("car");
        Assert.Null(editor.Document.FindRig("car"));

        // 손으로 쓴 문서의 순환·없는 세트는 펼칠 때 건너뛴다(던지지 않는다).
        var broken = new MotionDocument();
        broken.Rigs.Add(new RigAsset { Kind = "a", Nodes = { new RigNodeAsset { Id = "x", Set = "b" }, new RigNodeAsset { Id = "y", Set = "ghost" } } });
        broken.Rigs.Add(new RigAsset { Kind = "b", Nodes = { new RigNodeAsset { Id = "z", Set = "a" } } });
        Assert.True(RigAssets.Compose(Dump, broken).TryGetRig("a", out RigSchemaRigDto looped));
        Assert.Equal(new[] { "__root", "x", "x.z", "y" }, looped.nodes.Select(n => n.id));
    }
}
