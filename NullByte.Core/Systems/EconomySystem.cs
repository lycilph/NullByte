namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

public static class EconomySystem
{
    /// <summary>Botnet passive income per tick: 0.2 * lvl * 1.15^lvl.</summary>
    public static double IncomePerTick(GameState s)
    {
        int lvl = s.UpgradeLevels.GetValueOrDefault("botnet");
        return lvl == 0 ? 0 : 0.2 * lvl * Math.Pow(1.15, lvl);
    }

    public static void Tick(GameState s)
    {
        double income = IncomePerTick(s);
        if (income > 0)
            s.Money += income;
    }
}