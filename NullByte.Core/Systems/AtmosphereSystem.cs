namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

/// <summary>
/// Ambient world flavor: occasionally logs an idle line so the terminal
/// feels alive even when the player is idle. ~1 line per 3 minutes at 10 Hz.
/// </summary>
public static class AtmosphereSystem
{
    private const double idleChancePerTick = 0.0005; // Original chance 0.0005
    private static readonly Random rng = new();

    public static void Tick(GameState s, GameContent c)
    {
        if (c.Flavor.Idle.Count == 0)
            return;
        if (rng.NextDouble() < idleChancePerTick)
            s.Log(Flavor.Fill(Flavor.Pick(c.Flavor.Idle)));
    }
}