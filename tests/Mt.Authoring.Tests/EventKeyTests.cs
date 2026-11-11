using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Preview;
using Mt.Authoring.Sets;
using Mt.Authoring.Serialization;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

/// <summary>이벤트 키— "이 프레임에 소리".</summary>
public class EventKeyTests
{
    private static readonly BuiltSets BuiltSets = Fixtures.ActorBuiltSets();

    private static DocumentEditor NewEditor() => new(Fixtures.ActorDocument()); // nod 0.6초 · bow 0.4초

    [Fact]
    public void 이벤트는_시각_순으로_서고_명령_하나가_한_칸이다()
    {
        DocumentEditor editor = NewEditor();
        Assert.DoesNotContain("events", AuthoringDocumentJson.Write(editor.Document), StringComparison.Ordinal);

        Assert.Equal(0, editor.AddEventKey("nod", 0.3f, "flash", "땀"));
        Assert.Equal(0, editor.AddEventKey("nod", 0.1f, " sfx ", "pop")); // 앞에 끼어든다, 이름의 빈칸은 버린다
        Assert.Equal(2, editor.AddEventKey("nod", 0.3f, "flash_off"));     // 같은 시각이면 먼저 있던 것 뒤
        Assert.Equal(0, editor.AddEventKey("nod", -5f, "start"));          // 0 아래로 못 간다
        List<EventKeyAsset> events = editor.Document.Find("nod")!.Events;
        Assert.Equal(new[] { ("start", 0f), ("sfx", 0.1f), ("flash", 0.3f), ("flash_off", 0.3f) }, events.Select(e => (e.Name, e.T)));
        Assert.Null(events[3].Value);

        // 고치기: 시각이 바뀌면 자리가 옮겨지고 새 번호가 온다.
        long revision = editor.Revision;
        Assert.Equal(3, editor.UpdateEventKey("nod", 1, 0.5f, "sfx", "ding"));
        Assert.Equal(revision + 1, editor.Revision);
        Assert.Equal(("sfx", 0.5f, "ding"), (events[3].Name, events[3].T, events[3].Value));
        editor.Undo();
        Assert.Equal(("sfx", 0.1f, "pop"), (editor.Document.Find("nod")!.Events[1].Name, editor.Document.Find("nod")!.Events[1].T, editor.Document.Find("nod")!.Events[1].Value));

        editor.RemoveEventKey("nod", 0);
        Assert.Equal(3, editor.Document.Find("nod")!.Events.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.RemoveEventKey("nod", 9));
        Assert.Throws<KeyNotFoundException>(() => editor.AddEventKey("nope", 0f, "x"));

        // 저작본 왕복.
        string json = AuthoringDocumentJson.Write(editor.Document);
        Assert.Equal(json, AuthoringDocumentJson.Write(AuthoringDocumentJson.Read(json)));
        Assert.Equal(1, json.Split("\"events\"").Length - 1); // bow에는 없다
    }

    [Fact]
    public void 이벤트는_컷_파일의_events로_나가고_한글_값도_왕복한다()
    {
        // 컷의 타임라인에 놓은 이벤트 — 컷 파일에는 프레임으로 적힌다. 이벤트가 없으면 빈 목록이다.
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddCut("shot", 24);
        CutFileDto Built() => CutFileJson.Read(CutFileJson.Write(CutFileJson.Build(editor.Document, editor.Document.FindCut("shot")!, new Vec2(1920f, 1080f))));
        Assert.Empty(Built().events);

        editor.AddEventKey("shot", 0.25f, "flash", "땀 \"큰\"");
        editor.AddEventKey("shot", 1f, "done");
        Assert.Equal(new[] { (6f, "flash", "땀 \"큰\""), (24f, "done", (string?)null) }, Built().events.Select(e => (e.f, e.name, (string?)e.value)));
        Assert.Contains("\"value\": \"땀 \\\"큰\\\"\"", CutFileJson.Write(CutFileJson.Build(editor.Document, editor.Document.FindCut("shot")!, new Vec2(1920f, 1080f))), StringComparison.Ordinal);
    }

    [Fact]
    public void 규칙은_이름과_시각을_본다()
    {
        DocumentEditor editor = NewEditor();
        editor.AddEventKey("nod", 0.3f, "flash", "땀");
        Assert.Empty(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds));

        editor.AddEventKey("nod", 0.9f, "late"); // 길이(0.6) 밖
        Assert.Contains(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds), p => p.Blocking && p.Message.Contains("0~길이 밖", StringComparison.Ordinal));
        editor.Undo();

        editor.AddEventKey("nod", 0.2f, "Bad Name");
        ValidationProblem named = Assert.Single(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds));
        Assert.True(named.Blocking);
        Assert.Contains("이벤트 0: 이름", named.Message, StringComparison.Ordinal);
        Assert.Empty(ValidationFixes.Suggest(named)); // 애니메이션의 이름을 고치는 손은 이벤트의 이름에 서지 않는다
    }

    [Fact]
    public void 울리는_구간은_after_초과_until_이하라_같은_이벤트가_두_번_잡히지_않는다()
    {
        DocumentEditor editor = NewEditor();
        editor.AddEventKey("nod", 0.3f, "flash", "땀");
        editor.AddEventKey("nod", 0.6f, "done");
        TimelineAsset nod = editor.Document.Find("nod")!;

        Assert.Equal(new[] { "flash", "done" }, EventKeys.Between(nod, -1, 99).Select(e => e.Name));
        Assert.Equal("flash", Assert.Single(EventKeys.Between(nod, 0.0, 0.3)).Name);
        Assert.Empty(EventKeys.Between(nod, 0.3, 0.5));
        Assert.Equal("done", Assert.Single(EventKeys.Between(nod, 0.3, 0.6)).Name);
        Assert.Empty(EventKeys.Between(nod, 0.6, 99));
    }
}
