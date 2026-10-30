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

    /// <summary>Total ticks since the game started (100 ms per tick).</summary>
    public long GameTick { get; internal set; }

    /// <summary>Owned level per upgrade id, e.g. ["cpu"] = 3.</summary>
    public Dictionary<string, int> UpgradeLevels { get; internal set; } = [];
    
    /// <summary>Owned level per perk id (skill tree).</summary>
    public Dictionary<string, int> PerkLevels { get; internal set; } = [];

    /// <summary>Hacks currently running, with rewards frozen at start time.</summary>
    public List<ActiveHack> ActiveHacks { get; internal set; } = [];
    
    /// <summary>Lifetime totals for the stats page and achievement checks.</summary>
    public GameStats Stats { get; internal set; } = new();

    /// <summary>Ids of earned achievements.</summary>
    public HashSet<string> Achievements { get; internal set; } = [];

    /// <summary>
    /// Total events ever logged. The log list is a trimmed window, so a
    /// reader must track this total to know which entries it has already
    /// consumed after trims (a list index alone silently breaks at cap).
    /// </summary>
    public long EventsLogged { get; internal set; }

    /// <summary>Rolling event log; the UI reads this. Capped to 50 entries.</summary>
    public List<string> EventLog { get; } = [];

    internal void Log(string message)
    {
        EventLog.Add($"[t{GameTick}] {message}");
        EventsLogged++;
        if (EventLog.Count > 50)
            EventLog.RemoveAt(0);
    }
}

/// <summary>One in-flight hack. Serialized with the save.</summary>
public sealed class ActiveHack
{
    public string TargetId { get; init; } = "";
    public string TargetName { get; init; } = "";
    public int TicksRemaining { get; set; }
    public int TotalTicks { get; init; }
    public double MoneyReward { get; init; }
    public double XpReward { get; init; }
}

/// <summary>Lifetime counters; serialized with the save.</summary>
public sealed class GameStats
{
    [JsonInclude] public int TotalHacks { get; internal set; }
    [JsonInclude] public double MoneyEarned { get; internal set; }
    [JsonInclude] public int UpgradesBought { get; internal set; }

    /// <summary>Completed hacks per target id, e.g. ["city-bank"] = 12.</summary>
    [JsonInclude] public Dictionary<string, int> HacksPerTarget { get; internal set; } = [];
}