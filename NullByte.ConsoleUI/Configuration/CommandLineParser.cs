using System.Globalization;

namespace NullByte.ConsoleUI.Configuration;

public static class CommandLineParser
{
    public static CommandLineOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var startNewGame = false;
        var writeDefaultSettings = false;
        var enableEditor = false;
        int? editorPort = null;
        string? editorConnectionFilePath = null;
        string? saveDirectory = null;
        double? gameSpeedMultiplier = null;

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];

            switch (argument)
            {
                case "--new":
                    startNewGame = true;
                    break;

                case "--write-default-settings":
                    writeDefaultSettings = true;
                    break;

                case "--vscode":
                    enableEditor = true;
                    break;

                case "--vscode-port":
                    editorPort = ParseInt32(GetValue(args, ref i, argument), argument);
                    break;

                case "--vscode-file":
                    editorConnectionFilePath = GetValue(args, ref i, argument);
                    break;

                case "--save-directory":
                    saveDirectory = GetValue(args, ref i, argument);
                    break;

                case "--game-speed":
                    gameSpeedMultiplier = ParseDouble(GetValue(args, ref i, argument), argument);
                    break;

                default:
                    throw new ArgumentException($"Unknown command-line argument '{argument}'.");
            }
        }

        if (writeDefaultSettings && args.Length > 1)
            throw new ArgumentException("'--write-default-settings' cannot be combined with other command-line arguments.");

        return new CommandLineOptions
        {
            StartNewGame = startNewGame,
            WriteDefaultSettings = writeDefaultSettings,
            EnableEditor = enableEditor,
            EditorPort = editorPort,
            EditorConnectionFilePath = editorConnectionFilePath,
            SaveDirectory = saveDirectory,
            GameSpeedMultiplier = gameSpeedMultiplier
        };
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

    private static int ParseInt32(string value, string argument)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new ArgumentException($"Value '{value}' for '{argument}' must be an integer.");
    }

    private static double ParseDouble(string value, string argument)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new ArgumentException($"Value '{value}' for '{argument}' must be a number.");
    }
}