namespace NullByte.ConsoleUI.Configuration;

public sealed class SettingsValidationException : Exception
{
    public SettingsValidationException(IReadOnlyList<string> errors) : base(CreateMessage(errors))
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }

    private static string CreateMessage(IReadOnlyList<string> errors)
    {
        return "Invalid application settings:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, errors.Select(error => $"  - {error}"));
    }
}