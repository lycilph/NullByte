namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Identifies a dynamic source of tab-completion values.
/// </summary>
public enum CompletionKind
{
    None = 0,

    /// <summary>Discovered servers the player can connect to right now.</summary>
    ConnectableServer,
    /// <summary>Every server the player has discovered.</summary>
    KnownServer,
    /// <summary>Files on the player's current server.</summary>
    Filename,
    /// <summary>Combination of the Filename and KnownServer values.</summary>
    FilenameOrKnownServer,
    /// <summary>Existing save game.</summary>
    SaveGame,
    /// <summary>Existing botnet node numbers.</summary>
    BotnetNode
}