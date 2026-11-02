namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// HackSystem: mark heat & drain (Phase 4.5). A target is a resource, not a
// vending machine — every mark has a drainable money pool and a heat gauge.
// Hacks steal a % of the pool; heat rises (with a failure roll above 40);
// grow/scrub are the active counter-play ops. The pool only recovers via
// 'grow'; heat decays passively while no job runs on the mark.
// ---------------------------------------------------------------------------

public static class HackSystem
{
    // -- Tunables (Phase 4.5 spec) -------------------------------------------
    public const double StealBase = 0.12;         // hack steals 12% of the pool
    public const double HeatPerHack = 6;
    public const double HeatPerFailedHack = 10;   // getting traced makes it hotter
    public const double FailThreshold = 40;       // fail chance starts above this
    public const double FailMaxChance = 0.5;      // ...and caps at heat 100
    public const double GrowPctOfMax = 0.25;       // grow refills 25% of max
    public const double GrowHeat = 2;
    public const double GrowDurationMult = 1.5;
    public const double ScrubCool = 35;           // scrub dumps 35 heat
    public const double HeatDecayPerTick = 0.1;   // passive: -1/sec, paused while busy
    public const double TappedOutPct = 0.2;       // below this the mark is tapped out

    // DOCUMENTED ENGINE-RANDOMNESS EXCEPTION #2 (#1 = contract offers): the
    // heat failure roll is engine Random and changes gameplay outcomes.
    private static readonly Random Rng = new();

    // -- Target state ---------------------------------------------------------

    /// <summary>Max pool: content may pin it, else 8x the legacy flat reward
    /// (auto-tuned so the first hack pays ~what hacks paid before Phase 4.5).</summary>
    public static double MaxMoneyOf(TargetDef t)
        => t.MaxMoney > 0 ? t.MaxMoney : t.MoneyReward * 8;

    /// <summary>Lazy per-target state; first touch stocks the pool to max.</summary>
    public static TargetState StateFor(GameState s, TargetDef t)
    {
        if (!s.Targets.TryGetValue(t.Id, out var st))
        {
            st = new TargetState { CurrentMoney = MaxMoneyOf(t) };
            s.Targets[t.Id] = st;
        }
        return st;
    }

    /// <summary>Fail chance at a heat: 0 up to 40, 0.25 at 70, capped at 0.5.</summary>
    public static double FailChance(double heat)
        => Math.Clamp((heat - FailThreshold) / 120, 0, FailMaxChance);

    // -- Slots & duration -----------------------------------------------------
    
    public static double SpeedMult(GameState s)
        => Math.Pow(0.95, s.UpgradeLevels.GetValueOrDefault("cpu"));

    public static double HeatMult(GameState s, TargetDef t)
        => 1 + StateFor(s, t).Heat / 50;

    /// <summary>RAM gives one slot per level, plus Multitasking perk levels.</summary>
    public static int MaxConcurrentHacks(GameState s)
        => 1
           + s.UpgradeLevels.GetValueOrDefault("ram")
           + s.PerkLevels.GetValueOrDefault("multitask");

    /// <summary>Duration incl. heat: hot marks are slower to work on (+2%/heat).</summary>
    public static int HackDuration(TargetDef t, GameState s)
        => Math.Max(1, (int)Math.Round(
               t.DurationTicks
               * SpeedMult(s)
               * ProgressionSystem.SpeedMult(s)
               * PrestigeSystem.SpeedMult(s)
               * HeatMult(s, t)));

    // -- Starting jobs ---------------------------------------------------------

    public static void StartHack(GameState s, GameContent c, string id) => StartJob(s, c, id, JobKind.Hack);
    public static void StartGrow(GameState s, GameContent c, string id) => StartJob(s, c, id, JobKind.Grow);
    public static void StartScrub(GameState s, GameContent c, string id) => StartJob(s, c, id, JobKind.Scrub);

    private static void StartJob(GameState s, GameContent c, string targetId, JobKind kind)
    {
        var t = CanTouch(s, c, targetId);
        if (t is null)
            return;

        int duration = kind switch
        {
            JobKind.Grow => (int)Math.Ceiling(HackDuration(t, s) * GrowDurationMult),
            _ => HackDuration(t, s), // hack and scrub share the duration curve
        };

        double xp = kind switch
        {
            JobKind.Hack => t.XpReward,
            JobKind.Grow => 0.25 * t.XpReward, // growing is practice, not a payday
            _ => 0,
        } * ProgressionSystem.XpMult(s)
          * PrestigeSystem.XpMult(s);

        s.ActiveJobs.Add(new ActiveJob
        {
            TargetId = t.Id,
            TargetName = t.Name,
            Kind = kind,
            TicksRemaining = duration,
            TotalTicks = duration,
            XpReward = xp,
        });

        if (kind == JobKind.Hack)
        {
            string flavor = Flavor.Pick(c.Flavor.HackStart);
            s.Log(flavor.Length == 0
                ? $"Hack started on {t.Name} ({duration} ticks)."
                : $"{Flavor.Fill(flavor, target: t.Name)} ({duration} ticks)");
        }
        else if (kind == JobKind.Grow)
            s.Log($"Growing funds on {t.Name} ({duration} ticks) — tending the mark, +{GrowHeat:0.#} heat.");
        else
            s.Log($"Scrubbing traces on {t.Name} ({duration} ticks).");
    }

    /// <summary>Shared gate: known target, level, rig hardware, free job slot.</summary>
    private static TargetDef? CanTouch(GameState s, GameContent c, string targetId)
    {
        var t = c.FindTarget(targetId);
        if (t is null)
        {
            s.Log($"Unknown target '{targetId}'.");
            return null;
        }
        if (s.Level < t.MinLevel)
        {
            s.Log($"{t.Name}: security too high (needs level {t.MinLevel}).");
            return null;
        }
        // Rig thresholds: some targets need hardware, not just skill.
        if (t.RequiredCpu > 0 && s.UpgradeLevels.GetValueOrDefault("cpu") < t.RequiredCpu)
        {
            s.Log($"{t.Name}: intrusion trace too fast — needs CPU level {t.RequiredCpu}.");
            return null;
        }
        if (t.RequiredRam > 0 && s.UpgradeLevels.GetValueOrDefault("ram") < t.RequiredRam)
        {
            s.Log($"{t.Name}: payload too large — needs RAM level {t.RequiredRam}.");
            return null;
        }
        if (s.ActiveJobs.Count >= MaxConcurrentHacks(s))
        {
            s.Log("No free hack slot — buy a RAM Module.");
            return null;
        }
        return t;
    }

    // -- Tick -------------------------------------------------------------------
    public static void Tick(GameState s, GameContent c)
    {
        // Passive heat decay — paused while a job runs on the mark.
        foreach (var kv in s.Targets)
        {
            bool busy = false;
            foreach (var j in s.ActiveJobs)
                if (j.TargetId == kv.Key) { busy = true; break; }
            if (!busy)
                kv.Value.Heat = Math.Max(0, kv.Value.Heat - HeatDecayPerTick);
        }

        for (int i = s.ActiveJobs.Count - 1; i >= 0; i--)
        {
            var h = s.ActiveJobs[i];
            h.TicksRemaining--;
            if (h.TicksRemaining > 0)
                continue;

            Complete(s, c, h);
            s.ActiveJobs.RemoveAt(i);
        }
    }

    private static void Complete(GameState s, GameContent c, ActiveJob h)
    {
        var t = c.FindTarget(h.TargetId);
        if (t is null)
            return; // target removed from content while the job ran

        var ts = StateFor(s, t);
        double max = MaxMoneyOf(t);

        switch (h.Kind)
        {
            case JobKind.Grow:
                ts.CurrentMoney = Math.Min(max, ts.CurrentMoney + GrowPctOfMax * max);
                ts.Heat += GrowHeat;
                s.Xp += h.XpReward;
                s.Log($"Grew {h.TargetName}: pool now ${ts.CurrentMoney:0.#}/${max:0.#}.");
                break;

            case JobKind.Scrub:
                ts.Heat = Math.Max(0, ts.Heat - ScrubCool);
                s.Log($"Scrubbed {h.TargetName}: heat down to {ts.Heat:0.#}.");
                break;

            default: // JobKind.Hack
                // Failure roll (documented randomness exception): above 40 heat,
                // the intrusion can be traced — no payout, and it gets HOTTER.
                double chance = FailChance(ts.Heat);
                if (chance > 0 && Rng.NextDouble() < chance)
                {
                    ts.Heat += HeatPerFailedHack;
                    s.Log($"TRACED on {h.TargetName} — intrusion detected. No payout, heat now {ts.Heat:0.#}.");
                    return;
                }

                // Steal resolves at completion from the LIVE pool — parallel
                // jobs can't over-steal, and drained marks pay less each time.
                double steal = StealBase 
                    * ts.CurrentMoney
                    * ProgressionSystem.MoneyMult(s)
                    * PrestigeSystem.MoneyMult(s);
                ts.CurrentMoney -= steal;
                ts.Heat += HeatPerHack;

                s.Money += steal;
                s.Xp += h.XpReward;
                s.Stats.TotalHacks++;
                s.Stats.MoneyEarned += steal;
                s.Stats.HacksPerTarget[h.TargetId] =
                    s.Stats.HacksPerTarget.GetValueOrDefault(h.TargetId) + 1;

                string flavor = Flavor.Pick(c.Flavor.HackComplete);
                s.Log(flavor.Length == 0
                    ? $"Hack on {h.TargetName} complete: +${steal:0.##}, +{h.XpReward:0.#} XP"
                    : Flavor.Fill(flavor, target: h.TargetName, money: steal, xp: h.XpReward));

                s.Log($"Target heat: {ts.Heat}");

                if (ts.CurrentMoney < TappedOutPct * max)
                    s.Log($"{h.TargetName} is tapped out — pool at ${ts.CurrentMoney:0.#}. Grow it back or move on.");
                break;
        }
    }
}