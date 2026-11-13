namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Describes how one positional command argument can be tab-completed.
/// </summary>
public sealed record ArgumentCompletion
{
    /// <summary>
    /// Zero-based argument position after the command name.
    /// </summary>
    public required int ArgumentIndex { get; init; }

    /// <summary>
    /// Dynamic source of completion values.
    /// </summary>
    public CompletionKind Kind { get; init; }

    /// <summary>
    /// Static completion values.
    /// </summary>
    public IReadOnlyList<string> Values { get; init; } = [];
}