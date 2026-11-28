namespace NullByte.ConsoleUI.Configuration;

public static class SettingsLoader
{
    public static ApplicationOptions Load(string[] args)
    {
        return Load(args, SettingsFileLoader.GetSettingsFilePath());
    }

    internal static ApplicationOptions Load(string[] args, string settingsFilePath)
    {
        ArgumentNullException.ThrowIfNull(args);

        var commandLine = CommandLineParser.Parse(args);
        if (commandLine.WriteDefaultSettings)
        {
            return new ApplicationOptions
            {
                Settings = new AppSettings(),
                WriteDefaultSettings = true
            };
        }

        var settings = SettingsFileLoader.Load(settingsFilePath);
        SettingsValidator.Validate(settings);

        settings = ApplyOverrides(settings, commandLine);
        SettingsValidator.Validate(settings);

        return new ApplicationOptions
        {
            Settings = settings,
            StartNewGame = commandLine.StartNewGame,
            Freeplay = commandLine.Freeplay,
            WriteDefaultSettings = false
        };
    }

    private static AppSettings ApplyOverrides(AppSettings settings, CommandLineOptions commandLine)
    {
        var editor = settings.Editor;

        if (commandLine.EnableEditor)
        {
            editor = editor with
            {
                Enabled = true
            };
        }

        if (commandLine.EditorPort is not null)
        {
            editor = editor with
            {
                Port = commandLine.EditorPort
            };
        }

        if (commandLine.EditorConnectionFilePath is not null)
        {
            editor = editor with
            {
                ConnectionFilePath = commandLine.EditorConnectionFilePath
            };
        }

        var development = settings.Development;

        if (commandLine.GameSpeedMultiplier is not null)
        {
            development = development with
            {
                GameSpeedMultiplier = commandLine.GameSpeedMultiplier.Value
            };
        }

        return settings with
        {
            SaveDirectory = commandLine.SaveDirectory ?? settings.SaveDirectory,
            Editor = editor,
            Development = development
        };
    }
}