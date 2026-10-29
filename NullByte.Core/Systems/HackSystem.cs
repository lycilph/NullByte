namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

public static class HackSystem
{
    /// <summary>RAM: one slot per level, plus the base slot.</summary>
    public static int MaxConcurrentHacks(GameState s)
        => 1 + s.UpgradeLevels.GetValueOrDefault("ram");

    /// <summary>CPU: 5% faster hacks per level, multiplicative.</summary>
    public static int HackDuration(TargetDef t, GameState s)
        => Math.Max(1, (int)Math.Round(
               t.DurationTicks * Math.Pow(0.95, s.UpgradeLevels.GetValueOrDefault("cpu"))));

    public static void StartHack(GameState s, GameContent c, string targetId)
    {
        var t = c.FindTarget(targetId);
        if (t is null)
        {
            s.Log($"Unknown target '{targetId}'.");
            return;
        }
        if (s.Level < t.MinLevel)
        {
            s.Log($"{t.Name}: security too high (needs level {t.MinLevel}).");
            return;
        }
        if (s.ActiveHacks.Count >= MaxConcurrentHacks(s))
        {
            s.Log("No free hack slot — buy a RAM Module.");
            return;
        }

        int duration = HackDuration(t, s);
        s.ActiveHacks.Add(new ActiveHack
        {
            TargetId = t.Id,
            TargetName = t.Name,
            TicksRemaining = duration,
            TotalTicks = duration,
            MoneyReward = t.MoneyReward,
            XpReward = t.XpReward,
        });
        s.Log($"Hack started on {t.Name} ({duration} ticks).");
    }

    public static void Tick(GameState s)
    {
        for (int i = s.ActiveHacks.Count - 1; i >= 0; i--)
        {
            var h = s.ActiveHacks[i];
            h.TicksRemaining--;
            if (h.TicksRemaining <= 0)
            {
                s.Money += h.MoneyReward;
                s.Xp += h.XpReward;
                s.Log($"Hack on {h.TargetName} complete: +${h.MoneyReward:0.##}, +{h.XpReward:0.#} XP");
                s.ActiveHacks.RemoveAt(i);
            }
        }
    }
}