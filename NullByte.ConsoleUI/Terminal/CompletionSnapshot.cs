namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Immutable view of everything tab completion may suggest.
/// Built on the game thread, read on the terminal thread.
/// </summary>
public sealed record CompletionSnapshot(
    IReadOnlyList<CommandCompletionInfo> Commands,
    IReadOnlyDictionary<CompletionKind, IReadOnlyList<string>> Values)
{
    public static CompletionSnapshot Empty { get; } =
        new([], new Dictionary<CompletionKind, IReadOnlyList<string>>());

    public IReadOnlyList<string> GetValues(CompletionKind kind)
    {
        return Values.TryGetValue(kind, out var values)
             ? values : [];
    }
}

/// <summary>
/// Completion metadata for a registered command or alias.
/// </summary>
/// <param name="Name">
/// The command name or alias.
/// </param>
/// <param name="Completions">
/// Completion definitions for its arguments.
/// </param>
/// <param name="Hidden">
/// Hidden commands are never suggested, but can still provide
/// argument completion when explicitly entered.
/// </param>
public sealed record CommandCompletionInfo(
    string Name,
    IReadOnlyList<ArgumentCompletion> Completions,
    bool Hidden = false)
{
    public IReadOnlyList<string> Words { get; } = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}