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

    /// <summary>Total ticks since the game started (100 ms per tick).</summary>
    public long GameTick { get; internal set; }

    /// <summary>Owned level per upgrade id, e.g. ["cpu"] = 3.</summary>
    public Dictionary<string, int> UpgradeLevels { get; internal set; } = [];

    /// <summary>Hacks currently running, with rewards frozen at start time.</summary>
    public List<ActiveHack> ActiveHacks { get; internal set; } = [];
    
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