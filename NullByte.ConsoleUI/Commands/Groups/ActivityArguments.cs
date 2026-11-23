namespace NullByte.ConsoleUI.Commands.Groups;

/// <summary>What the player asked for with <c>study</c> or <c>train</c>.</summary>
/// <param name="Course">The course, or null to list the courses.</param>
/// <param name="Facility">The university or gym, or null for the default one.</param>
/// <param name="Background">Whether to start in the background.</param>
public readonly record struct CourseRequest(string? Course, string? Facility, bool Background);

/// <summary>
/// Parses the arguments of <c>study</c> and <c>train</c>: up to two names and a
/// <c>-b</c> flag that may be anywhere.
/// </summary>
public static class ActivityArguments
{
    public const string BackgroundFlag = "-b";
    public const string BackgroundFlagLong = "--background";

    public static bool TryParse(IReadOnlyList<string> arguments, out CourseRequest request, out string error)
    {
        request = default;
        error = string.Empty;

        var names = new List<string>();
        var background = false;

        foreach (var argument in arguments)
        {
            if (argument is BackgroundFlag or BackgroundFlagLong)
            {
                background = true;
            }
            else if (argument.StartsWith('-'))
            {
                error = $"Unknown option '{argument}'. The only option is {BackgroundFlag} (start in the background).";
                return false;
            }
            else
            {
                names.Add(argument);
            }
        }

        if (names.Count > 2)
        {
            error = "Too many arguments.";
            return false;
        }

        request = new CourseRequest(names.ElementAtOrDefault(0), names.ElementAtOrDefault(1), background);
        return true;
    }
}