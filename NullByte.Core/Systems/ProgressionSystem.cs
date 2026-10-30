namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

public static class ProgressionSystem
{
    /// <summary>XP needed to go from 'currentLevel' to currentLevel+1.</summary>
    public static double XpForLevelUp(int currentLevel)
        => 100 * Math.Pow(1.5, currentLevel - 1);

    /// <summary>Cumulative XP needed to reach 'level' (from level 1).</summary>
    public static double TotalXpForLevel(int level)
    {
        double total = 0;
        for (int l = 1; l < level; l++)
            total += XpForLevelUp(l);
        return total;
    }

    public static void Tick(GameState s)
    {
        while (s.Xp >= TotalXpForLevel(s.Level + 1))
        {
            s.Level++;
            s.SkillPoints++;
            s.Log($"Level up! Now level {s.Level}. +1 skill point (see 'perks').");
        }
    }
}