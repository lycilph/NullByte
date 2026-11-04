using System.Collections.Concurrent;

namespace NullByte.Core.Domain;

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
    public GameStatistics Statistics { get; internal set; } = new();

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

    /// <summary> Live per-target state (money pool, heat), keyed by target id.</summary>
    public Dictionary<string, TargetState> Targets { get; internal set; } = [];

    /// <summary>Event log; the UI reads and consumes this.</summary>
    public ConcurrentQueue<string> EventLog { get; } = [];

    internal void Log(string message) => EventLog.Enqueue($"[t{GameTick}] {message}");
}