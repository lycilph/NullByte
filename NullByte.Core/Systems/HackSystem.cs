namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

public static class HackSystem
{
    public static int RigRam(GameState s) => s.UpgradeLevels.GetValueOrDefault("ram");
    public static int RigCpu(GameState s) => s.UpgradeLevels.GetValueOrDefault("cpu");
    
    public static double SpeedMult(GameState s)
        => Math.Pow(0.95, s.UpgradeLevels.GetValueOrDefault("cpu"));

    /// <summary>RAM gives one slot per level, plus Multitasking perk levels.</summary>
    public static int MaxConcurrentHacks(GameState s) 
        => 1 + RigRam(s) + s.PerkLevels.GetValueOrDefault("multitask");

    /// <summary>CPU: 5%/lvl; Overclock perk: 3%/lvl; Neural implant: 3%/lvl.</summary>
    public static int HackDuration(TargetDef t, GameState s)
        => Math.Max(1, (int)Math.Round(
            t.DurationTicks 
            * SpeedMult(s)
            * ProgressionSystem.SpeedMult(s)
            * PrestigeSystem.SpeedMult(s)));

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

        // Rig thresholds: some targets need hardware, not just skill.
        if (t.RequiredCpu > 0 && RigCpu(s) < t.RequiredCpu)
        {
            s.Log($"{t.Name}: intrusion trace too fast — needs CPU level {t.RequiredCpu}.");
            return;
        }
        if (t.RequiredRam > 0 && RigRam(s) < t.RequiredRam)
        {
            s.Log($"{t.Name}: payload too large — needs RAM level {t.RequiredRam}.");
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
            MoneyReward = t.MoneyReward 
                * ProgressionSystem.MoneyMult(s)
                * PrestigeSystem.MoneyMult(s),
            XpReward = t.XpReward 
                * ProgressionSystem.XpMult(s)
                * PrestigeSystem.XpMult(s),
        });

        string flavor = Flavor.Pick(c.Flavor.HackStart);
        s.Log(flavor.Length == 0
            ? $"Hack started on {t.Name} ({duration} ticks)."
            : $"{Flavor.Fill(flavor, target: t.Name)} ({duration} ticks)");
    }

    public static void Tick(GameState s, GameContent c)
    {
        for (int i = s.ActiveHacks.Count - 1; i >= 0; i--)
        {
            var h = s.ActiveHacks[i];
            h.TicksRemaining--;
            if (h.TicksRemaining <= 0)
            {
                s.Money += h.MoneyReward;
                s.Xp += h.XpReward;

                // Update stats
                s.Stats.TotalHacks++;
                s.Stats.MoneyEarned += h.MoneyReward;
                s.Stats.HacksPerTarget[h.TargetId] =
                    s.Stats.HacksPerTarget.GetValueOrDefault(h.TargetId) + 1;

                string flavor = Flavor.Pick(c.Flavor.HackComplete);
                s.Log(flavor.Length == 0
                    ? $"Hack on {h.TargetName} complete: +${h.MoneyReward:0.##}, +{h.XpReward:0.#} XP"
                    : Flavor.Fill(flavor, target: h.TargetName, money: h.MoneyReward, xp: h.XpReward));

                s.ActiveHacks.RemoveAt(i);
            }
        }
    }
}