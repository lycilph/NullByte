namespace NullByte.ConsoleUI.Configuration;

public sealed record ApplicationOptions
{
    public required AppSettings Settings { get; init; }

    public bool StartNewGame { get; init; }
    public bool WriteDefaultSettings { get; init; }

}