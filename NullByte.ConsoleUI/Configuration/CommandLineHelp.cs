namespace NullByte.ConsoleUI.Configuration;

public static class CommandLineHelp
{
    public static bool IsRequested(string[] args) =>
        CommandLineOptionDefinitions.IsHelpRequested(args);

    public static string GetUsageText()
    {
        var lines = new List<string>
        {
            "NullByte - an incremental hacking game",
            "",
            "Usage:",
            "  dotnet run --project NullByte.ConsoleUI [options]",
            "",
            "Options:"
        };

        var options = CommandLineOptionDefinitions.All;
        var nameWidth = options.Max(option =>
            option.DisplayName.Length + (option.ValueName is null ? 0 : option.ValueName.Length + 3));

        foreach (var option in options)
        {
            var name = option.ValueName is null
                ? option.DisplayName
                : $"{option.DisplayName} <{option.ValueName}>";

            lines.Add($"  {name.PadRight(nameWidth + 2)}{option.Description}");
        }

        lines.Add("");
        lines.Add("Help is displayed before settings are loaded, and takes precedence over other arguments.");

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
