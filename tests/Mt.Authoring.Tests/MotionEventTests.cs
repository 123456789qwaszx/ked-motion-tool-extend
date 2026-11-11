using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Rig;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

/// <summary>이벤트 키(<c>PLAN-parts.md</c> §2.5) — "이 프레임에 소리".</summary>
public class MotionEventTests
{
    private static readonly RigSchemaFile RigFile = Fixtures.ActorRigFile();

    private static MotionEditor NewEditor() => new(Fixtures.ActorDocument()); // nod 0.6초 · bow 0.4초

    [Fact]
    public void 이벤트는_시각_순으로_서고_명령_하나가_한_칸이다()
    {
        MotionEditor editor = NewEditor();
        Assert.DoesNotContain("events", MotionDocumentJson.Write(editor.Document), StringComparison.Ordinal);

        Assert.Equal(0, editor.AddMotionEvent("nod", 0.3f, "flash", "땀"));
        Assert.Equal(0, editor.AddMotionEvent("nod", 0.1f, " sfx ", "pop")); // 앞에 끼어든다, 이름의 빈칸은 버린다
        Assert.Equal(2, editor.AddMotionEvent("nod", 0.3f, "flash_off"));     // 같은 시각이면 먼저 있던 것 뒤
        Assert.Equal(0, editor.AddMotionEvent("nod", -5f, "start"));          // 0 아래로 못 간다
        List<MotionEventAsset> events = editor.Document.Find("nod")!.Events;
        Assert.Equal(new[] { ("start", 0f), ("sfx", 0.1f), ("flash", 0.3f), ("flash_off", 0.3f) }, events.Select(e => (e.Name, e.T)));
        Assert.Null(events[3].Value);

        // 고치기: 시각이 바뀌면 자리가 옮겨지고 새 번호가 온다.
        long revision = editor.Revision;
        Assert.Equal(3, editor.UpdateMotionEvent("nod", 1, 0.5f, "sfx", "ding"));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(("sfx", 0.5f, "ding"), (events[3].Name, events[3].T, events[3].Value));
        editor.Undo();
        Assert.Equal(("sfx", 0.1f, "pop"), (editor.Document.Find("nod")!.Events[1].Name, editor.Document.Find("nod")!.Events[1].T, editor.Document.Find("nod")!.Events[1].Value));

        editor.RemoveMotionEvent("nod", 0);
        Assert.Equal(3, editor.Document.Find("nod")!.Events.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.RemoveMotionEvent("nod", 9));
        Assert.Throws<KeyNotFoundException>(() => editor.AddMotionEvent("nope", 0f, "x"));

        // 저작본 왕복.
        string json = MotionDocumentJson.Write(editor.Document);
        Assert.Equal(json, MotionDocumentJson.Write(MotionDocumentJson.Read(json)));
        Assert.Equal(1, json.Split("\"events\"").Length - 1); // bow에는 없다
    }

    [Fact]
    public void 이벤트는_컷_파일의_events로_나가고_한글_값도_왕복한다()
    {
        // 컷의 타임라인에 놓은 이벤트 — 컷 파일에는 프레임으로 적힌다. 이벤트가 없으면 빈 목록이다.
        var editor = new MotionEditor(new MotionDocument());
        editor.AddCut("shot", 24);
        CutFileDto Built() => CutFileJson.Read(CutFileJson.Write(CutFileJson.Build(editor.Document, editor.Document.FindCut("shot")!, new Vec2(1920f, 1080f))));
        Assert.Empty(Built().events);

        editor.AddMotionEvent("shot", 0.25f, "flash", "땀 \"큰\"");
        editor.AddMotionEvent("shot", 1f, "done");
        Assert.Equal(new[] { (6f, "flash", "땀 \"큰\""), (24f, "done", (string?)null) }, Built().events.Select(e => (e.f, e.name, (string?)e.value)));
        Assert.Contains("\"value\": \"땀 \\\"큰\\\"\"", CutFileJson.Write(CutFileJson.Build(editor.Document, editor.Document.FindCut("shot")!, new Vec2(1920f, 1080f))), StringComparison.Ordinal);
    }

    [Fact]
    public void 규칙은_이름과_시각을_본다()
    {
        MotionEditor editor = NewEditor();
        editor.AddMotionEvent("nod", 0.3f, "flash", "땀");
        Assert.Empty(MotionValidator.Validate(editor.Document, RigFile.NodeIds));

        editor.AddMotionEvent("nod", 0.9f, "late"); // 길이(0.6) 밖
        Assert.Contains(MotionValidator.Validate(editor.Document, RigFile.NodeIds), p => p.Blocking && p.Message.Contains("0~길이 밖", StringComparison.Ordinal));
        editor.Undo();

        editor.AddMotionEvent("nod", 0.2f, "Bad Name");
        MotionProblem named = Assert.Single(MotionValidator.Validate(editor.Document, RigFile.NodeIds));
        Assert.True(named.Blocking);
        Assert.Contains("이벤트 0: 이름", named.Message, StringComparison.Ordinal);
        Assert.Empty(MotionFixes.Suggest(named)); // 애니메이션의 이름을 고치는 손은 이벤트의 이름에 서지 않는다
    }

    [Fact]
    public void 울리는_구간은_after_초과_until_이하라_같은_이벤트가_두_번_잡히지_않는다()
    {
        MotionEditor editor = NewEditor();
        editor.AddMotionEvent("nod", 0.3f, "flash", "땀");
        editor.AddMotionEvent("nod", 0.6f, "done");
        MotionAsset nod = editor.Document.Find("nod")!;

        Assert.Equal(new[] { "flash", "done" }, MotionEvents.Between(nod, -1, 99).Select(e => e.Name));
        Assert.Equal("flash", Assert.Single(MotionEvents.Between(nod, 0.0, 0.3)).Name);
        Assert.Empty(MotionEvents.Between(nod, 0.3, 0.5));
        Assert.Equal("done", Assert.Single(MotionEvents.Between(nod, 0.3, 0.6)).Name);
        Assert.Empty(MotionEvents.Between(nod, 0.6, 99));
    }
}
