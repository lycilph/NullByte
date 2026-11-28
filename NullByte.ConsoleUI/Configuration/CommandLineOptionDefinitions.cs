using System.Globalization;

namespace NullByte.ConsoleUI.Configuration;

internal sealed record CommandLineOptionDefinition(
    string[] Names,
    string? ValueName,
    string Description,
    Action<CommandLineOptionsBuilder, string?>? Apply = null,
    bool IsHelp = false)
{
    public bool RequiresValue => ValueName is not null;
    public string DisplayName => string.Join(", ", Names);
}

internal static class CommandLineOptionDefinitions
{
    public static IReadOnlyList<CommandLineOptionDefinition> All { get; } =
    [
        new(["help", "--help"], null, "Display this help and exit.", IsHelp: true),
        new(["--new"], null, "Start a new game instead of resuming the latest save.",
            (options, _) => options.StartNewGame = true),
        new(["--freeplay"], null, "Start a new game in Freeplay mode. Requires --new.",
            (options, _) => options.Freeplay = true),
        new(["--write-default-settings"], null, "Write the default application settings file and exit.",
            (options, _) => options.WriteDefaultSettings = true),
        new(["--vscode"], null, "Enable the VS Code editor integration.",
            (options, _) => options.EnableEditor = true),
        new(["--vscode-port"], "port", "Set the VS Code integration port.",
            (options, value) => options.EditorPort = ParseInt32(value!, "--vscode-port")),
        new(["--vscode-file"], "path", "Set the VS Code connection file path.",
            (options, value) => options.EditorConnectionFilePath = value),
        new(["--save-directory"], "path", "Set the directory used for save files.",
            (options, value) => options.SaveDirectory = value),
        new(["--game-speed"], "multiplier", "Override the game speed multiplier.",
            (options, value) => options.GameSpeedMultiplier = ParseDouble(value!, "--game-speed"))
    ];

    public static bool IsHelpRequested(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(argument => All.Any(option =>
            option.IsHelp && option.Names.Contains(argument, StringComparer.Ordinal)));
    }

    public static CommandLineOptionDefinition? Find(string argument) =>
        All.FirstOrDefault(option => !option.IsHelp
            && option.Names.Contains(argument, StringComparer.Ordinal));

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

internal sealed class CommandLineOptionsBuilder
{
    public bool StartNewGame { get; set; }
    public bool Freeplay { get; set; }
    public bool WriteDefaultSettings { get; set; }
    public bool EnableEditor { get; set; }
    public int? EditorPort { get; set; }
    public string? EditorConnectionFilePath { get; set; }
    public string? SaveDirectory { get; set; }
    public double? GameSpeedMultiplier { get; set; }

    public CommandLineOptions Build() => new()
    {
        StartNewGame = StartNewGame,
        Freeplay = Freeplay,
        WriteDefaultSettings = WriteDefaultSettings,
        EnableEditor = EnableEditor,
        EditorPort = EditorPort,
        EditorConnectionFilePath = EditorConnectionFilePath,
        SaveDirectory = SaveDirectory,
        GameSpeedMultiplier = GameSpeedMultiplier
    };
}
