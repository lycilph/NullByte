namespace NullByte.ConsoleUI.Configuration;

public static class CommandLineParser
{
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
}
