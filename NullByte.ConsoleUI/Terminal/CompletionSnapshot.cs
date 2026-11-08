namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Immutable view of everything tab completion may suggest.
/// Built on the game thread, read on the terminal thread.
/// </summary>
public sealed record CompletionSnapshot(
    IReadOnlyList<CommandCompletionInfo> Commands,
    IReadOnlyList<string> ConnectableServers,
    IReadOnlyList<string> KnownServers,
    IReadOnlyList<string> BotnetSubcommands,
    IReadOnlyList<string> BotnetNodes,
    IReadOnlyList<string> BotnetUpgradeKinds)
{
    public static CompletionSnapshot Empty { get; } = new([], [], [], [], [], []);
}

/// <param name="Name">The command name or alias.</param>
/// <param name="ArgumentCompletion">What its first argument completes to.</param>
/// <param name="Hidden">Hidden commands are never suggested, but still drive argument completion.</param>
public sealed record CommandCompletionInfo(
    string Name,
    ArgumentCompletionKind ArgumentCompletion,
    bool Hidden = false);