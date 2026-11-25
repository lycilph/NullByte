namespace NullByte.ConsoleUI.Configuration;

public sealed record AppSettings
{
    public string SaveDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NullByte", "saves");

    public EditorSettings Editor { get; init; } = new();

    public DevelopmentSettings Development { get; init; } = new();

    public StartupSettings Startup { get; init; } = new();
}

public sealed record EditorSettings
{
    public bool Enabled { get; init; }

    public int? Port { get; init; }

    public string? ConnectionFilePath { get; init; }
}

public sealed record DevelopmentSettings
{
    public double GameSpeedMultiplier { get; init; } = 1.0;
}

public sealed record StartupSettings
{
    public bool ShowBootSequence { get; init; } = true;
}
