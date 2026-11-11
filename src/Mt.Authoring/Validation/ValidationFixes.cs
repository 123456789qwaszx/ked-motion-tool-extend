using Mt.Authoring.Editing;
using Mt.Authoring.Model;

namespace Mt.Authoring.Validation;

/// <summary>검증 문제를 한 번에 고치는 돕는 손. 각각 편집기 명령 하나 = 되돌리기 하나.</summary>
public enum ValidationFix
{
    /// <summary>이름을 규칙(소문자·숫자·_)에 맞게, 겹치지 않게 고친다. 빈 이름은 timeline.</summary>
    NameToValid,
}

public static class ValidationFixes
{
    public static string Label(ValidationFix fix) => fix switch
    {
        ValidationFix.NameToValid => "이름 고치기 (소문자·숫자·_)",
        _ => fix.ToString(),
    };

    /// <summary><see cref="DocumentValidator"/>의 문구에서 고칠 손을 고른다. 문구가 바뀌면 여기만 따라간다.</summary>
    public static IReadOnlyList<ValidationFix> Suggest(ValidationProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        // 이벤트의 이름은 이벤트 줄에서 고친다 — 여기는 애니메이션의 이름만.
        return problem.Message.StartsWith("이름은 [a-z0-9_]+ 여야 한다", StringComparison.Ordinal)
            ? new[] { ValidationFix.NameToValid }
            : Array.Empty<ValidationFix>();
    }

    /// <summary>고친다. 할 것이 없으면 false(편집기 조작도 없다).</summary>
    public static bool Apply(DocumentEditor editor, string timelineName, ValidationFix fix)
    {
        ArgumentNullException.ThrowIfNull(editor);
        TimelineAsset timeline = editor.Document.Find(timelineName) ?? throw new KeyNotFoundException($"타임라인 '{timelineName}'이 없다.");

        switch (fix)
        {
            case ValidationFix.NameToValid:
            {
                string fixedName = NameRules.Unique(editor.Document, NameRules.Sanitize(timeline.Name), except: timeline.Name);

                if (fixedName == timeline.Name)
                {
                    return false;
                }

                editor.RenameTimeline(timelineName, fixedName);
                return true;
            }

            default:
                return false;
        }
    }
}
