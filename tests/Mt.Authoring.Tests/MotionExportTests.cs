using Ked.Presentation.Core;
using Mt.Authoring.Editing;
using Mt.Authoring.Model;
using Mt.Authoring.Rig;
using Mt.Authoring.Validation;

namespace Mt.Authoring.Tests;

public class MotionExportTests
{
    private static readonly RigSchemaFile RigFile = RigSchemaFile.Load(Fixtures.PathOf("rig-schemas.json"));

    [Fact]
    public void 내보내기_필터는_고른_모션만_검증하고_쓴다()
    {
        MotionDocument document = Fixtures.NodBowDocument();
        var editor = new MotionEditor(document);
        editor.AddMotion("robot_wave", "robot", 1f);
        editor.AddChannel("robot_wave", "Robot_Arm", MotionChannelKind.Position);

        Assert.False(MotionExport.TryExport(document, RigFile.NodeIds, out _, out IReadOnlyList<MotionProblem> all));
        Assert.Contains(all, p => p.MotionName == "robot_wave");

        Assert.True(MotionExport.TryExport(document, RigFile.NodeIds, out string json, out IReadOnlyList<MotionProblem> problems, m => m.Rig == "character"));
        Assert.Empty(problems);
        Assert.Contains("\"nod\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("robot_wave", json, StringComparison.Ordinal);
    }
}
