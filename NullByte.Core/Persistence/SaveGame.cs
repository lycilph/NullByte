using System.Text.Json.Serialization;

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
    public const int CurrentVersion = 6;

    public required int Version { get; init; }
    public required DateTimeOffset SavedAtUtc { get; init; }
    /// <summary>The game clock when the save was taken.</summary>
    public required TimeSpan GameTime { get; init; }
    public required PlayerSave Player { get; init; }
    public required IReadOnlyList<ServerSave> Servers { get; init; }
    public required IReadOnlyList<ConnectionSave> Connections { get; init; }
    public required BotnetSave Botnet { get; init; }
    /// <summary>Jobs held and reputation earned. Added in version 4.</summary>
    public required EmploymentSave Employment { get; init; }
    /// <summary>What the player was doing when the game was saved, or null if nothing. Added in version 4.</summary>
    public ActivitySave? Activity { get; init; }
    /// <summary>Every directory, including empty ones. Files restore the directories above them on their own.</summary>
    public required IReadOnlyList<DirectorySave> Directories { get; init; }
    public required IReadOnlyList<FileSave> Files { get; init; }
    /// <summary>Scripts that were running. They are started again on load.</summary>
    public required IReadOnlyList<ProcessSave> Processes { get; init; }
}

public sealed record PlayerSave
{
    public required string HomeServerId { get; init; }
    public required string CurrentServerId { get; init; }
    public required double Money { get; init; }
    /// <summary>
    /// Experience per skill, by lowercase skill name (<c>hacking</c>, <c>strength</c>, ...). Skills that are missing have none.
    /// Before version 4 this was a single <c>HackingExperience</c> number.
    /// </summary>
    public required IReadOnlyDictionary<string, double> Skills { get; init; }
    public required IReadOnlyList<string> DiscoveredServerIds { get; init; }
}

/// <summary>
/// A server with both its fixed properties and its current state. Saves are
/// self-contained, so loading never depends on the shipped network file.
/// UsedRam is not stored: it is the sum of the restarted processes.
/// </summary>
public sealed record ServerSave
{
    public required string Id { get; init; }
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
    /// <summary>Whether the player can connect directly to the server. Added in version 5.</summary>
    public required bool HasBackdoor { get; init; }
}

/// <summary>
/// A connection between two servers. Connections are serialized in canonical order so that equivalent networks produce identical save files.
/// </summary>
public sealed record ConnectionSave
{
    public required string FromServerId { get; init; }
    public required string ToServerId { get; init; }
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
    public required string ServerId { get; init; }
    /// <summary>The path relative to the server root, for example "tools/net".</summary>
    public required string Path { get; init; }
}

public sealed record FileSave
{
    public required string ServerId { get; init; }
    /// <summary>The path of the file relative to the server root. Before version 3 this was always a plain name.</summary>
    public required string Name { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Modified { get; init; }
}

public sealed record ProcessSave
{
    public required string Filename { get; init; }
    public required string ServerId { get; init; }
    public required IReadOnlyList<string> Args { get; init; }
    public required int Threads { get; init; }
}

/// <summary>
/// The player's working life. Companies are referred to by name, so a save never depends on the
/// ranks or rates in the shipped data, only on the names existing.
/// </summary>
public sealed record EmploymentSave
{
    public required IReadOnlyList<JobSave> Jobs { get; init; }
    public required IReadOnlyList<ReputationSave> Reputation { get; init; }
}

public sealed record JobSave
{
    public required string Company { get; init; }
    public required string Career { get; init; }
    /// <summary>The position on the career path, counting from zero at the entry rank.</summary>
    public required int RankIndex { get; init; }
}

public sealed record ReputationSave
{
    public required string Company { get; init; }
    public required double Amount { get; init; }
}

/// <summary>
/// What the player was doing. Exactly one of <see cref="Class"/>, <see cref="Job"/> and
/// <see cref="Crime"/> is set. A loaded game resumes the activity in the background, so the
/// mode is not saved.
/// </summary>
public sealed record ActivitySave
{
    public required ActivityProgressSave Progress { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClassActivitySave? Class { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JobActivitySave? Job { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CrimeActivitySave? Crime { get; init; }
}

/// <summary>What the activity has earned so far, so that its summary carries on after a load.</summary>
public sealed record ActivityProgressSave
{
    public required TimeSpan Elapsed { get; init; }
    public required double NetMoney { get; init; }
    /// <summary>Experience gained so far, by lowercase skill name.</summary>
    public required IReadOnlyDictionary<string, double> Experience { get; init; }
    public required double ReputationGained { get; init; }
    public required int Attempts { get; init; }
    public required int Successes { get; init; }
}

public sealed record ClassActivitySave
{
    public required string Course { get; init; }
    public required string Facility { get; init; }
}

/// <summary>
/// The rank is not repeated here: it is the one in <see cref="EmploymentSave.Jobs"/>.
/// </summary>
public sealed record JobActivitySave
{
    public required string Company { get; init; }
    public required string Career { get; init; }
}

public sealed record CrimeActivitySave
{
    public required string Crime { get; init; }
    public required bool Repeat { get; init; }
    /// <summary>How far into the current attempt the player was, in game seconds.</summary>
    public required double CycleSeconds { get; init; }
}