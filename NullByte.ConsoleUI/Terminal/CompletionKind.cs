namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Identifies a dynamic source of tab-completion values.
/// </summary>
public enum CompletionKind
{
    None = 0,

    /// <summary>
    /// Discovered servers the player can connect to right now.
    /// </summary>
    ConnectableServer,

    /// <summary>
    /// Every server the player has discovered.
    /// </summary>
    KnownServer,

    /// <summary>
    /// Fon the player's current server.
    /// </summary>
    Filename,

    /// <summary>
    /// Existing botnet node numbers.
    /// </summary>
    BotnetNode
}