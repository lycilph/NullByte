namespace NullByte.ConsoleUI.Configuration;

public static class CommandLineParser
{
    public static bool IsRequested(string[] args) =>
        CommandLineOptionDefinitions.IsHelpRequested(args);

    public static CommandLineOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = new CommandLineOptionsBuilder();

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            var definition = CommandLineOptionDefinitions.Find(argument);

            if (definition is null)
                throw new ArgumentException($"Unknown command-line argument '{argument}'.");

            string? value = null;
            if (definition.RequiresValue)
                value = GetValue(args, ref i, argument);

            definition.Apply!(options, value);
        }

        if (options.WriteDefaultSettings && args.Length > 1)
            throw new ArgumentException("'--write-default-settings' cannot be combined with other command-line arguments.");

        if (options.Freeplay && !options.StartNewGame)
            throw new ArgumentException("'--freeplay' requires '--new'.");

        return options.Build();
    }

    private static string GetValue(string[] args, ref int index, string argument)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Command-line argument '{argument}' requires a value.");

        var value = args[++index];

        if (value.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Command-line argument '{argument}' requires a value.");

        return value;
    }

    public static string GetUsageText()
    {
        var lines = new List<string>
        {
            "NullByte - an incremental hacking game",
            "",
            "Usage:",
            "  NullByte.ConsoleUI [options]",
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
