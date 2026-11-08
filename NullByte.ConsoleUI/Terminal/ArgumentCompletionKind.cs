namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Describes what the first argument of a command can be tab-completed to.
/// </summary>
public enum ArgumentCompletionKind
{
    /// <summary>The command's arguments are not completed.</summary>
    None = 0,

    /// <summary>Discovered servers the player can connect to right now.</summary>
    ConnectableServer,

    /// <summary>Every server the player has discovered.</summary>
    KnownServer,

    /// <summary>Sub commands for the botnet command.</summary>
    BotnetSubcommand
}