namespace NullByte.ConsoleUI.Configuration;

public sealed record CommandLineOptions
{
    public bool StartNewGame { get; init; }
    public bool WriteDefaultSettings { get; init; }
    public bool EnableEditor { get; init; }
    public int? EditorPort { get; init; }
    public string? EditorConnectionFilePath { get; init; }
    public string? SaveDirectory { get; init; }
    public double? GameSpeedMultiplier { get; init; }
}