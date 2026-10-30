namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Achievements: content-defined goals checked against lifetime stats, once
// per tick. Because stats update inside Tick, achievements unlock correctly
// during offline catch-up too. Each achievement is awarded exactly once.
// ---------------------------------------------------------------------------

public static class AchievementsSystem
{
    private const string targetHackPrefix = "targetHacks:";

    public static double StatValue(GameState s, string key)
    {
        if (key.StartsWith(targetHackPrefix))
            return s.Stats.HacksPerTarget.GetValueOrDefault(key[targetHackPrefix.Length..]);

        return key switch
        {
            "hacks" => s.Stats.TotalHacks,
            "moneyEarned" => s.Stats.MoneyEarned,
            "level" => s.Level,
            "upgradesBought" => s.Stats.UpgradesBought,
            "contractsCompleted" => s.Stats.ContractsCompleted,
            "contractsFailed" => s.Stats.ContractsFailed,
            _ => 0,
        };
    }

    public static void Tick(GameState s, GameContent c)
    {
        foreach (var def in c.Achievements)
        {
            if (s.Achievements.Contains(def.Id))
                continue;

            if (StatValue(s, def.Stat) >= def.Goal)
            {
                s.Achievements.Add(def.Id);
                s.Log($"ACHIEVEMENT: {def.Name} — {def.Description}.");
            }
        }
    }
}