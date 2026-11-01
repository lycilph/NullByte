using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace NullByte.Core;

/// <summary>
/// All mutable game data. Systems read and write this; the UI never touches
/// it directly. Plain data so it can be serialized into the save file.
/// </summary>

public sealed class GameState
{
    public double Money { get; internal set; }
    public double Xp { get; internal set; }
    public int Level { get; internal set; } = 1;

    /// <summary>Spendable points earned on level-up; buy perks with these.</summary>
    public int SkillPoints { get; internal set; }

    /// <summary>0-days: prestige currency earned by burning your identity.</summary>
    public long ZeroDays { get; internal set; }
    
    /// <summary>Total ticks since the game started (100 ms per tick).</summary>
    public long GameTick { get; internal set; }

    /// <summary>Owned level per upgrade id, e.g. ["cpu"] = 3.</summary>
    public Dictionary<string, int> UpgradeLevels { get; internal set; } = [];
    
    /// <summary>Owned level per perk id (skill tree).</summary>
    public Dictionary<string, int> PerkLevels { get; internal set; } = [];

    /// <summary>Lifetime totals for the stats page and achievement checks.</summary>
    public GameStats Stats { get; internal set; } = new();

    /// <summary>Ids of earned achievements.</summary>
    public HashSet<string> Achievements { get; internal set; } = [];
    
    /// <summary>Contracts currently open for acceptance.</summary>
    public List<ContractOfferState> ContractOffers { get; internal set; } = [];

    /// <summary>Accepted contracts running against their deadlines.</summary>
    public List<ActiveContractState> ActiveContracts { get; internal set; } = [];

    /// <summary>Owned level per implant id (prestige upgrades; survive resets).</summary>
    public Dictionary<string, int> PrestigeLevels { get; internal set; } = [];

    /// <summary>Jobs currently running on marks (hack / grow / scrub).</summary>
    public List<ActiveJob> ActiveJobs { get; internal set; } = [];

    /// <summary>
    /// Live per-target state (money pool, heat), keyed by target id.
    /// Lazily initialized on first touch — see HackSystem.StateFor.
    /// </summary>
    public Dictionary<string, TargetState> Targets { get; internal set; } = [];

    /// <summary>Event log; the UI reads and consumes this.</summary>
    public ConcurrentQueue<string> EventLog { get; } = [];

    internal void Log(string message) => EventLog.Enqueue($"[t{GameTick}] {message}");
}

/// <summary>What a job does to its target.</summary>
public enum JobKind { Hack, Grow, Scrub }

/// <summary>
/// One in-flight job on a mark (hack, grow, or scrub). Money is NOT frozen
/// here anymore — steals are computed at completion from the target's live
/// pool, so parallel jobs can't over-steal (Phase 4.5). Serialized with the save.
/// </summary>
public sealed class ActiveJob
{
    public string TargetId { get; init; } = "";
    public string TargetName { get; init; } = "";
    public JobKind Kind { get; init; }
    public int TicksRemaining { get; set; }
    public int TotalTicks { get; init; }
    public double XpReward { get; init; } // hack & grow pay XP; scrub pays none
}

/// <summary>
/// Live state of one target: a drainable money pool and a heat gauge
/// (Phase 4.5). The pool only recovers via 'grow' (an active op) — heat
/// decays passively when no job runs on the mark. Wiped on identity burn.
/// </summary>
public sealed class TargetState
{
    [JsonInclude] public double CurrentMoney { get; internal set; }
    [JsonInclude] public double Heat { get; internal set; }
}

/// <summary>Lifetime counters; serialized with the save.</summary>
public sealed class GameStats
{
    [JsonInclude] public int TotalHacks { get; internal set; }
    [JsonInclude] public double MoneyEarned { get; internal set; }
    [JsonInclude] public int UpgradesBought { get; internal set; }

    /// <summary>Completed hacks per target id, e.g. ["city-bank"] = 12.</summary>
    [JsonInclude] public Dictionary<string, int> HacksPerTarget { get; internal set; } = [];

    [JsonInclude] public int ContractsCompleted { get; internal set; }
    [JsonInclude] public int ContractsFailed { get; internal set; }

    [JsonInclude] public int PrestigeCount { get; internal set; }
}


/// <summary>A contract on the board, not yet accepted. Serialized with the save.</summary>
public sealed class ContractOfferState
{
    [JsonInclude] public string DefId { get; internal set; } = "";
}

/// <summary>
/// An accepted contract: deadline countdown plus the stat baseline
/// snapshot taken at acceptance (progress = current - baseline).
/// Serialized with the save.
/// </summary>
public sealed class ActiveContractState
{
    [JsonInclude] public string DefId { get; internal set; } = "";
    [JsonInclude] public int DeadlineRemaining { get; internal set; }
    [JsonInclude] public double StartStat { get; internal set; }
}