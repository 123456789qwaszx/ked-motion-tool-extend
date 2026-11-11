using Mt.Authoring.Editing;
using Mt.Authoring.Model;

namespace Mt.Authoring.Validation;

/// <summary>검증 문제를 한 번에 고치는 돕는 손. 각각 편집기 명령 하나 = 되돌리기 하나.</summary>
public enum MotionFix
{
    /// <summary>이름을 규칙(소문자·숫자·_)에 맞게, 겹치지 않게 고친다. 빈 이름은 motion.</summary>
    NameToValid,
}

public static class MotionFixes
{
    public static string Label(MotionFix fix) => fix switch
    {
        MotionFix.NameToValid => "이름 고치기 (소문자·숫자·_)",
        _ => fix.ToString(),
    };

    /// <summary><see cref="MotionValidator"/>의 문구에서 고칠 손을 고른다. 문구가 바뀌면 여기만 따라간다.</summary>
    public static IReadOnlyList<MotionFix> Suggest(MotionProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        // 이벤트의 이름은 이벤트 줄에서 고친다 — 여기는 애니메이션의 이름만.
        return problem.Message.StartsWith("이름은 [a-z0-9_]+ 여야 한다", StringComparison.Ordinal)
            ? new[] { MotionFix.NameToValid }
            : Array.Empty<MotionFix>();
    }

    /// <summary>고친다. 할 것이 없으면 false(편집기 조작도 없다).</summary>
    public static bool Apply(MotionEditor editor, string motionName, MotionFix fix)
    {
        ArgumentNullException.ThrowIfNull(editor);
        MotionAsset motion = editor.Document.Find(motionName) ?? throw new KeyNotFoundException($"모션 '{motionName}'이 없다.");

        switch (fix)
        {
            case MotionFix.NameToValid:
            {
                string fixedName = MotionNames.Unique(editor.Document, MotionNames.Sanitize(motion.Name), except: motion.Name);

                if (fixedName == motion.Name)
                {
                    return false;
                }

                editor.RenameMotion(motionName, fixedName);
                return true;
            }

            default:
                return false;
        }
    }
}
