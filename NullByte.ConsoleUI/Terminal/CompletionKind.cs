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
    /// <summary>Files on the current server, with directories to step through. Completed one path segment at a time.</summary>
    Filename,
    /// <summary>Directories on the current server, completed one path segment at a time.</summary>
    Directory,
    /// <summary>Same as Filename. Kept so that commands can state their intent.</summary>
    FilenameOrDirectory,
    /// <summary>Combination of the Filename values and the KnownServer values.</summary>
    FilenameOrKnownServer,
    /// <summary>Existing save game.</summary>
    SaveGame,
    /// <summary>Existing botnet node numbers.</summary>
    BotnetNode,
    /// <summary>Cloud servers owned by the player.</summary>
    CloudServer,
    /// <summary>University courses.</summary>
    UniversityCourse,
    /// <summary>Universities.</summary>
    University,
    /// <summary>Gym courses.</summary>
    GymCourse,
    /// <summary>Gyms.</summary>
    Gym,
    /// <summary>Companies.</summary>
    Company,
    /// <summary>Careers offered by companies.</summary>
    Career,
    /// <summary>Crimes.</summary>
    Crime
}