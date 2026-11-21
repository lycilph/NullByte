namespace NullByte.Core.Persistence;

/// <summary>
/// The on-disk form of a game: a plain data contract, deliberately separate from the
/// domain model so that refactoring the domain cannot silently change the save format.
/// </summary>
/// <remarks>
/// Any change to the shape of this file needs a new <see cref="CurrentVersion"/> and an
/// <see cref="ISaveMigration"/> that upgrades the previous version. The golden-file test
/// fails when the format changes by accident.
/// </remarks>
public sealed record SaveGame
{
    /// <summary>The format version this build writes, and the newest one it can read.</summary>
    public const int CurrentVersion = 3;

    public required int Version { get; init; }
    public required DateTimeOffset SavedAtUtc { get; init; }
    /// <summary>The game clock when the save was taken.</summary>
    public required TimeSpan GameTime { get; init; }
    public required PlayerSave Player { get; init; }
    public required IReadOnlyList<ServerSave> Servers { get; init; }
    public required IReadOnlyList<ConnectionSave> Connections { get; init; }
    public required BotnetSave Botnet { get; init; }
    /// <summary>Every directory, including empty ones. Files restore the directories above them on their own.</summary>
    public required IReadOnlyList<DirectorySave> Directories { get; init; }
    public required IReadOnlyList<FileSave> Files { get; init; }
    /// <summary>Scripts that were running. They are started again on load.</summary>
    public required IReadOnlyList<ProcessSave> Processes { get; init; }
}

public sealed record PlayerSave
{
    public required string HomeHostname { get; init; }
    public required string CurrentHostname { get; init; }
    public required double Money { get; init; }
    public required double HackingExperience { get; init; }
    public required IReadOnlyList<string> DiscoveredHostnames { get; init; }
}

/// <summary>
/// A server with both its fixed properties and its current state. Saves are
/// self-contained, so loading never depends on the shipped network file.
/// UsedRam is not stored: it is the sum of the restarted processes.
/// </summary>
public sealed record ServerSave
{
    public required string Hostname { get; init; }
    public required double MaximumMoney { get; init; }
    public required double Money { get; init; }
    public required double BaseDifficulty { get; init; }
    public required double MinimumSecurity { get; init; }
    public required double Security { get; init; }
    public required int RequiredHackingLevel { get; init; }
    public required double GrowthParameter { get; init; }
    public required double MaximumRam { get; init; }
    public required bool HasRootAccess { get; init; }
}

/// <summary>
/// A connection between two servers. Connections are serialized in canonical order so that equivalent networks produce identical save files.
/// </summary>
public sealed record ConnectionSave
{
    public required string From { get; init; }
    public required string To { get; init; }
}

public sealed record BotnetSave
{
    public required double TotalEarned { get; init; }
    public required IReadOnlyList<BotnetNodeSave> Nodes { get; init; }
}

public sealed record BotnetNodeSave
{
    public required int Level { get; init; }
    public required int Ram { get; init; }
    public required int Cores { get; init; }
}

public sealed record DirectorySave
{
    public required string Hostname { get; init; }
    /// <summary>The path relative to the server root, for example "tools/net".</summary>
    public required string Path { get; init; }
}

public sealed record FileSave
{
    public required string Hostname { get; init; }
    /// <summary>The path of the file relative to the server root. Before version 3 this was always a plain name.</summary>
    public required string Name { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Modified { get; init; }
}

public sealed record ProcessSave
{
    public required string Filename { get; init; }
    public required string Hostname { get; init; }
    public required IReadOnlyList<string> Args { get; init; }
    public required int Threads { get; init; }
}
