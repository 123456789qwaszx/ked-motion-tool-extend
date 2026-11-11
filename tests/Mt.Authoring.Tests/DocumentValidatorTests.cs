using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Sets;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

public class DocumentValidatorTests
{
    private static readonly BuiltSets BuiltSets = Fixtures.ActorBuiltSets();
    private const string MoveY = "CharacterPortrait_Track_Move_Y";

    [Fact]
    public void nod_bow는_세운_계층으로_검증해도_문제가_0건이다()
    {
        Assert.Empty(DocumentValidator.Validate(Fixtures.ActorDocument(), BuiltSets.NodeIds, BuiltSets.AlphaNodeIds));
    }

    [Fact]
    public void 세트에_없는_오브젝트는_거부하고_세운_계층이_없으면_그_검사는_생략한다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddTimeline("a", Fixtures.Actor, 1f);
        editor.AddChannel("a", "NoSuchNode", ChannelKind.Position);
        editor.SetKeys("a", "NoSuchNode", ChannelKind.Position, Axis.X, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.5f, 5f), Fixtures.Key(1f, 0f) });

        ValidationProblem problem = Assert.Single(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds));
        Assert.True(problem.Blocking);
        Assert.Contains("이 세트·컷에 없는 오브젝트", problem.Message, StringComparison.Ordinal);
        Assert.Empty(DocumentValidator.Validate(editor.Document));
    }

    [Fact]
    public void 모르는_setName는_문서_수준_문제다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddTimeline("a", "robot", 1f);

        ValidationProblem problem = Assert.Single(DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds));
        Assert.Contains("'robot'라는 세트나 컷이 없다", problem.Message, StringComparison.Ordinal);
        Assert.Contains(Fixtures.Actor, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 같은_이름이_둘이면_문제다()
    {
        AuthoringDocument document = Fixtures.ActorDocument();
        document.Timelines.Add(new TimelineAsset { Name = document.Timelines[0].Name, Set = Fixtures.Actor, Duration = 1f });

        Assert.Contains(DocumentValidator.Validate(document), p => p.Blocking && p.Message == "같은 이름이 둘이다");
    }

    [Fact]
    public void 키는_처음과_끝에_없어도_하나뿐이어도_되고_값은_묻지_않으며_길이_밖의_키는_막지_않고_알린다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        editor.AddSet(Fixtures.ActorSet());
        editor.AddTimeline("a", Fixtures.Actor, 1f);
        editor.AddChannel("a", MoveY, ChannelKind.Position);
        IReadOnlyList<ValidationProblem> Problems() => DocumentValidator.Validate(editor.Document, BuiltSets.NodeIds, BuiltSets.AlphaNodeIds);

        // 첫 키가 0초에 없고 마지막 키가 길이에 없다 — 앞뒤로는 그 키의 값이 이어진다. 값이 어디서 시작하고 끝나든 작가의 뜻이다.
        editor.SetKeys("a", MoveY, ChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0.25f, 3f), Fixtures.Key(0.5f, -10f) });
        Assert.Empty(Problems());

        // 키가 하나뿐이어도 된다. 키가 다 지워져 빈 채널도 문제가 아니다(컷 파일에 아무것도 안 나간다).
        editor.SetKeys("a", MoveY, ChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0.5f, -10f) });
        Assert.Empty(Problems());
        editor.SetKeys("a", MoveY, ChannelKind.Position, Axis.Y, Array.Empty<KeyAsset>());
        Assert.Empty(Problems());

        // 길이를 줄여 키가 길이 밖에 남았다 — 막지 않고 알린다(그 자리는 재생되지 않는다).
        editor.SetKeys("a", MoveY, ChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.4f, -10f), Fixtures.Key(0.9f, 0f) });
        editor.SetDuration("a", 0.5f);
        ValidationProblem notice = Assert.Single(Problems());
        Assert.False(notice.Blocking);
        Assert.Contains("밖에 키가 1개", notice.Message, StringComparison.Ordinal);
        Assert.Empty(DocumentValidator.Blocking(Problems()));
        Assert.Empty(ValidationFixes.Suggest(notice));

        editor.SetDuration("a", 1f);
        Assert.Empty(Problems());
    }

    [Fact]
    public void 컷_파일로_나갈_수_없는_모양은_막고_문제는_처음_것_하나가_아니라_전부_낸다()
    {
        // 편집기는 이런 모양을 만들지 않는다 — 손으로 쓴 문서나 옛 파일에서 온다.
        var timeline = new TimelineAsset { Name = "Bad Name", Set = Fixtures.Actor, Duration = 0f };
        KeyAsset K(float t, float v) => new() { T = t, V = v };
        timeline.Channels.Add(new ChannelAsset { Object = MoveY, Kind = ChannelKind.Position, X = new ComponentCurve { Keys = { K(0.5f, 1f), K(0.2f, 2f) } }, Z = new ComponentCurve { Keys = { K(0f, 0f) } } });
        timeline.Channels.Add(new ChannelAsset { Object = MoveY, Kind = ChannelKind.Position, Y = new ComponentCurve { Keys = { K(0f, float.NaN) } } });
        timeline.Channels.Add(new ChannelAsset { Object = "", Kind = ChannelKind.Scale });
        var document = new AuthoringDocument { Sets = { Fixtures.ActorSet() }, Timelines = { timeline } };

        List<string> messages = DocumentValidator.Validate(document, BuiltSets.NodeIds, BuiltSets.AlphaNodeIds).Where(p => p.Blocking).Select(p => p.Message).ToList();

        Assert.Contains(messages, m => m.StartsWith("이름은 [a-z0-9_]+ 여야 한다", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("길이가 0보다 커야 한다", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("채널 0: X: 키 시각이 오름차순이 아니다", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("채널 0: Position에는 Z 성분이 없다", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("채널 1: 같은 노드·종류의 채널이 둘이다", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("채널 1: Y: 키의 시각·값·기울기는 수여야 한다", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("채널 2: 노드가 비어 있다", StringComparison.Ordinal));
        Assert.Equal(7, messages.Count);
    }
}

public class ValidationFixesTests
{
    [Fact]
    public void 문구에서_돕는_손을_고른다_이름을_고치는_손_하나다()
    {
        Assert.Equal(new[] { ValidationFix.NameToValid }, ValidationFixes.Suggest(new ValidationProblem("Nod", "이름은 [a-z0-9_]+ 여야 한다 ('Nod')")));
        Assert.Empty(ValidationFixes.Suggest(new ValidationProblem("a", "같은 이름이 둘이다")));
        Assert.Empty(ValidationFixes.Suggest(new ValidationProblem("a", "채널 0: Y: 길이(0~0.5초) 밖에 키가 1개 있다 — 그 자리는 재생되지 않는다", Blocking: false)));
        Assert.Equal("이름 고치기 (소문자·숫자·_)", ValidationFixes.Label(ValidationFix.NameToValid));
    }
}

public class NameRulesTests
{
    [Fact]
    public void 이름은_소문자_숫자_밑줄만이고_고치면_규칙에_맞고_겹치지_않는다()
    {
        Assert.True(NameRules.IsValid("nod_2"));
        Assert.False(NameRules.IsValid(""));
        Assert.False(NameRules.IsValid("Nod"));
        Assert.False(NameRules.IsValid("끄덕"));

        Assert.Equal("nod bow".Replace(" ", "_"), NameRules.Sanitize("Nod Bow"));
        Assert.Equal("timeline", NameRules.Sanitize(""));
        Assert.Equal("timeline", NameRules.Sanitize("끄덕"));
        Assert.Equal("a_b", NameRules.Sanitize("--a--b--"));

        var document = new AuthoringDocument();
        document.Timelines.Add(new TimelineAsset { Name = "timeline", Set = Fixtures.Actor, Duration = 1 });
        document.Timelines.Add(new TimelineAsset { Name = "timeline_2", Set = Fixtures.Actor, Duration = 1 });
        Assert.Equal("timeline_3", NameRules.Unique(document, "timeline"));
        Assert.Equal("timeline", NameRules.Unique(document, "timeline", except: "timeline"));
    }

    [Fact]
    public void 편집기는_규칙_밖_이름을_거부하고_돕는_손이_빈_이름을_고친다()
    {
        var editor = new DocumentEditor(new AuthoringDocument());
        Assert.Throws<InvalidOperationException>(() => editor.AddTimeline("", Fixtures.Actor, 1f));
        Assert.Throws<InvalidOperationException>(() => editor.AddTimeline("Nod", Fixtures.Actor, 1f));
        editor.AddTimeline("nod", Fixtures.Actor, 1f);
        Assert.Throws<InvalidOperationException>(() => editor.RenameTimeline("nod", "Nod!"));

        // 이미 빈 이름으로 저장된 문서(예전 버전)는 돕는 손으로 고친다.
        var document = new AuthoringDocument();
        document.Timelines.Add(new TimelineAsset { Name = "", Set = Fixtures.Actor, Duration = 1 });
        var old = new DocumentEditor(document);
        ValidationProblem problem = Assert.Single(DocumentValidator.Validate(old.Document), p => p.Message.Contains("이름은", StringComparison.Ordinal));
        Assert.Equal(new[] { ValidationFix.NameToValid }, ValidationFixes.Suggest(problem));

        Assert.True(ValidationFixes.Apply(old, problem.TimelineName, ValidationFix.NameToValid));

        Assert.Equal("timeline", old.Document.Timelines[0].Name);
        Assert.DoesNotContain(DocumentValidator.Validate(old.Document), p => p.Message.Contains("이름은", StringComparison.Ordinal));
    }
}

public class ToolSettingsTests
{
    [Fact]
    public void 설정은_왕복하고_없으면_기본값이고_깨지면_기본값에_사유를_남긴다()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ked-motion-tool-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, Mt.Authoring.Settings.ToolSettings.FileName);

        try
        {
            var settings = Mt.Authoring.Settings.ToolSettings.Load(path);
            Assert.True(settings.SnapEnabled);
            Assert.Equal(TangentMode.Auto, settings.DefaultTangent);

            settings.SnapEnabled = false;
            settings.DefaultTangent = TangentMode.Plateau;
            settings.AutoSaveMinutes = 1;
            settings.Touch("/a/one.cut-authoring.json");
            settings.Touch("/a/two.cut-authoring.json");
            settings.Touch("/a/one.cut-authoring.json");
            settings.Save(path);

            var back = Mt.Authoring.Settings.ToolSettings.Load(path);
            Assert.False(back.SnapEnabled);
            Assert.Equal(TangentMode.Plateau, back.DefaultTangent);
            Assert.Equal(1, back.AutoSaveMinutes);
            Assert.Equal(2, back.RecentFiles.Count);
            Assert.EndsWith("one.cut-authoring.json", back.RecentFiles[0]);
            Assert.Contains("\"plateau\"", File.ReadAllText(path), StringComparison.Ordinal);

            File.WriteAllText(path, "{ broken");
            var broken = Mt.Authoring.Settings.ToolSettings.Load(path);
            Assert.True(broken.SnapEnabled);
            Assert.NotNull(broken.LoadProblem);

            // 걷어 낸 설정 키가 든 옛 파일도 그대로 열린다 — 그 키는 무시된다.
            File.WriteAllText(path, """{ "snapEnabled": false, "showObjects": true, "collapsedChannelBoxGroups": ["placement"] }""");
            var old = Mt.Authoring.Settings.ToolSettings.Load(path);
            Assert.Null(old.LoadProblem);
            Assert.False(old.SnapEnabled);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void 최근_파일은_여덟_개까지다()
    {
        var settings = new Mt.Authoring.Settings.ToolSettings();

        for (int i = 0; i < 12; i++)
        {
            settings.Touch($"/x/{i}.json");
        }

        Assert.Equal(Mt.Authoring.Settings.ToolSettings.MaxRecentFiles, settings.RecentFiles.Count);
        Assert.EndsWith("11.json", settings.RecentFiles[0]);
    }
}
