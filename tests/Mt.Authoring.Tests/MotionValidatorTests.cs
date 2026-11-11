using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Rig;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

public class MotionValidatorTests
{
    private static readonly RigSchemaFile RigFile = Fixtures.ActorRigFile();
    private const string MoveY = "CharacterPortrait_Track_Move_Y";

    [Fact]
    public void nod_bow는_세운_리그로_검증해도_문제가_0건이다()
    {
        Assert.Empty(MotionValidator.Validate(Fixtures.ActorDocument(), RigFile.NodeIds, RigFile.AlphaNodeIds));
    }

    [Fact]
    public void 세트에_없는_오브젝트는_거부하고_세운_리그가_없으면_그_검사는_생략한다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddMotion("a", Fixtures.Actor, 1f);
        editor.AddChannel("a", "NoSuchNode", MotionChannelKind.Position);
        editor.SetKeys("a", "NoSuchNode", MotionChannelKind.Position, Axis.X, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.5f, 5f), Fixtures.Key(1f, 0f) });

        MotionProblem problem = Assert.Single(MotionValidator.Validate(editor.Document, RigFile.NodeIds));
        Assert.True(problem.Blocking);
        Assert.Contains("이 세트·컷에 없는 오브젝트", problem.Message, StringComparison.Ordinal);
        Assert.Empty(MotionValidator.Validate(editor.Document));
    }

    [Fact]
    public void 모르는_rigKind는_문서_수준_문제다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddMotion("a", "robot", 1f);

        MotionProblem problem = Assert.Single(MotionValidator.Validate(editor.Document, RigFile.NodeIds));
        Assert.Contains("rigKind 'robot'", problem.Message, StringComparison.Ordinal);
        Assert.Contains(Fixtures.Actor, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 같은_이름이_둘이면_문제다()
    {
        MotionDocument document = Fixtures.ActorDocument();
        document.Motions.Add(new MotionAsset { Name = document.Motions[0].Name, Rig = Fixtures.Actor, Duration = 1f });

        Assert.Contains(MotionValidator.Validate(document), p => p.Blocking && p.Message == "같은 이름이 둘이다");
    }

    [Fact]
    public void 키는_처음과_끝에_없어도_하나뿐이어도_되고_값은_묻지_않으며_길이_밖의_키는_막지_않고_알린다()
    {
        var editor = new MotionEditor(new MotionDocument());
        editor.AddRig(Fixtures.ActorRig());
        editor.AddMotion("a", Fixtures.Actor, 1f);
        editor.AddChannel("a", MoveY, MotionChannelKind.Position);
        IReadOnlyList<MotionProblem> Problems() => MotionValidator.Validate(editor.Document, RigFile.NodeIds, RigFile.AlphaNodeIds);

        // 첫 키가 0초에 없고 마지막 키가 길이에 없다 — 앞뒤로는 그 키의 값이 이어진다. 값이 어디서 시작하고 끝나든 작가의 뜻이다.
        editor.SetKeys("a", MoveY, MotionChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0.25f, 3f), Fixtures.Key(0.5f, -10f) });
        Assert.Empty(Problems());

        // 키가 하나뿐이어도 된다. 키가 다 지워져 빈 채널도 문제가 아니다(컷 파일에 아무것도 안 나간다).
        editor.SetKeys("a", MoveY, MotionChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0.5f, -10f) });
        Assert.Empty(Problems());
        editor.SetKeys("a", MoveY, MotionChannelKind.Position, Axis.Y, Array.Empty<KeyAsset>());
        Assert.Empty(Problems());

        // 길이를 줄여 키가 길이 밖에 남았다 — 막지 않고 알린다(그 자리는 재생되지 않는다).
        editor.SetKeys("a", MoveY, MotionChannelKind.Position, Axis.Y, new[] { Fixtures.Key(0f, 0f), Fixtures.Key(0.4f, -10f), Fixtures.Key(0.9f, 0f) });
        editor.SetDuration("a", 0.5f);
        MotionProblem notice = Assert.Single(Problems());
        Assert.False(notice.Blocking);
        Assert.Contains("밖에 키가 1개", notice.Message, StringComparison.Ordinal);
        Assert.Empty(MotionValidator.Blocking(Problems()));
        Assert.Empty(MotionFixes.Suggest(notice));

        editor.SetDuration("a", 1f);
        Assert.Empty(Problems());
    }

    [Fact]
    public void 컷_파일로_나갈_수_없는_모양은_막고_문제는_처음_것_하나가_아니라_전부_낸다()
    {
        // 편집기는 이런 모양을 만들지 않는다 — 손으로 쓴 문서나 옛 파일에서 온다.
        var motion = new MotionAsset { Name = "Bad Name", Rig = Fixtures.Actor, Duration = 0f };
        KeyAsset K(float t, float v) => new() { T = t, V = v };
        motion.Channels.Add(new ChannelAsset { Node = MoveY, Kind = MotionChannelKind.Position, X = new ComponentCurve { Keys = { K(0.5f, 1f), K(0.2f, 2f) } }, Z = new ComponentCurve { Keys = { K(0f, 0f) } } });
        motion.Channels.Add(new ChannelAsset { Node = MoveY, Kind = MotionChannelKind.Position, Y = new ComponentCurve { Keys = { K(0f, float.NaN) } } });
        motion.Channels.Add(new ChannelAsset { Node = "", Kind = MotionChannelKind.Scale });
        var document = new MotionDocument { Rigs = { Fixtures.ActorRig() }, Motions = { motion } };

        List<string> messages = MotionValidator.Validate(document, RigFile.NodeIds, RigFile.AlphaNodeIds).Where(p => p.Blocking).Select(p => p.Message).ToList();

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

public class MotionFixesTests
{
    [Fact]
    public void 문구에서_돕는_손을_고른다_이름을_고치는_손_하나다()
    {
        Assert.Equal(new[] { MotionFix.NameToValid }, MotionFixes.Suggest(new MotionProblem("Nod", "이름은 [a-z0-9_]+ 여야 한다 ('Nod')")));
        Assert.Empty(MotionFixes.Suggest(new MotionProblem("a", "같은 이름이 둘이다")));
        Assert.Empty(MotionFixes.Suggest(new MotionProblem("a", "채널 0: Y: 길이(0~0.5초) 밖에 키가 1개 있다 — 그 자리는 재생되지 않는다", Blocking: false)));
        Assert.Equal("이름 고치기 (소문자·숫자·_)", MotionFixes.Label(MotionFix.NameToValid));
    }
}

public class MotionNamesTests
{
    [Fact]
    public void 이름은_소문자_숫자_밑줄만이고_고치면_규칙에_맞고_겹치지_않는다()
    {
        Assert.True(MotionNames.IsValid("nod_2"));
        Assert.False(MotionNames.IsValid(""));
        Assert.False(MotionNames.IsValid("Nod"));
        Assert.False(MotionNames.IsValid("끄덕"));

        Assert.Equal("nod bow".Replace(" ", "_"), MotionNames.Sanitize("Nod Bow"));
        Assert.Equal("motion", MotionNames.Sanitize(""));
        Assert.Equal("motion", MotionNames.Sanitize("끄덕"));
        Assert.Equal("a_b", MotionNames.Sanitize("--a--b--"));

        var document = new MotionDocument();
        document.Motions.Add(new MotionAsset { Name = "motion", Rig = Fixtures.Actor, Duration = 1 });
        document.Motions.Add(new MotionAsset { Name = "motion_2", Rig = Fixtures.Actor, Duration = 1 });
        Assert.Equal("motion_3", MotionNames.Unique(document, "motion"));
        Assert.Equal("motion", MotionNames.Unique(document, "motion", except: "motion"));
    }

    [Fact]
    public void 편집기는_규칙_밖_이름을_거부하고_돕는_손이_빈_이름을_고친다()
    {
        var editor = new MotionEditor(new MotionDocument());
        Assert.Throws<InvalidOperationException>(() => editor.AddMotion("", Fixtures.Actor, 1f));
        Assert.Throws<InvalidOperationException>(() => editor.AddMotion("Nod", Fixtures.Actor, 1f));
        editor.AddMotion("nod", Fixtures.Actor, 1f);
        Assert.Throws<InvalidOperationException>(() => editor.RenameMotion("nod", "Nod!"));

        // 이미 빈 이름으로 저장된 문서(예전 버전)는 돕는 손으로 고친다.
        var document = new MotionDocument();
        document.Motions.Add(new MotionAsset { Name = "", Rig = Fixtures.Actor, Duration = 1 });
        var old = new MotionEditor(document);
        MotionProblem problem = Assert.Single(MotionValidator.Validate(old.Document), p => p.Message.Contains("이름은", StringComparison.Ordinal));
        Assert.Equal(new[] { MotionFix.NameToValid }, MotionFixes.Suggest(problem));

        Assert.True(MotionFixes.Apply(old, problem.MotionName, MotionFix.NameToValid));

        Assert.Equal("motion", old.Document.Motions[0].Name);
        Assert.DoesNotContain(MotionValidator.Validate(old.Document), p => p.Message.Contains("이름은", StringComparison.Ordinal));
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
            settings.Touch("/a/one.motion-authoring.json");
            settings.Touch("/a/two.motion-authoring.json");
            settings.Touch("/a/one.motion-authoring.json");
            settings.Save(path);

            var back = Mt.Authoring.Settings.ToolSettings.Load(path);
            Assert.False(back.SnapEnabled);
            Assert.Equal(TangentMode.Plateau, back.DefaultTangent);
            Assert.Equal(1, back.AutoSaveMinutes);
            Assert.Equal(2, back.RecentFiles.Count);
            Assert.EndsWith("one.motion-authoring.json", back.RecentFiles[0]);
            Assert.Contains("\"plateau\"", File.ReadAllText(path), StringComparison.Ordinal);

            File.WriteAllText(path, "{ broken");
            var broken = Mt.Authoring.Settings.ToolSettings.Load(path);
            Assert.True(broken.SnapEnabled);
            Assert.NotNull(broken.LoadProblem);

            // 걷어 낸 설정 키가 든 옛 파일도 그대로 열린다 — 그 키는 무시된다.
            File.WriteAllText(path, """{ "snapEnabled": false, "showRigNodes": true, "collapsedChannelBoxGroups": ["placement"] }""");
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
