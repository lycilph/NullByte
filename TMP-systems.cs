using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullbyte.Core;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Flavor: random terminal lines from content JSON. Pure presentation —
// never affects gameplay numbers, so engine randomness stays harmless.
// ---------------------------------------------------------------------------

public static class Flavor
{
    private static readonly Random Rng = new();

    public static string Pick(IReadOnlyList<string> lines)
        => lines.Count == 0 ? "" : lines[Rng.Next(lines.Count)];

    public static string Fill(string template,
                              string target = "",
                              double money = 0,
                              double xp = 0)
        => template
            .Replace("{target}", target)
            .Replace("{money}", money.ToString("0.##"))
            .Replace("{xp}", xp.ToString("0.#"));
}

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

    /// <summary>RAM gives one slot per level, plus Multitasking perk levels.</summary>
    public static int MaxConcurrentHacks(GameState s)
        => 1 + s.UpgradeLevels.GetValueOrDefault("ram")
            + s.PerkLevels.GetValueOrDefault("multitask");

    /// <summary>Duration incl. heat: hot marks are slower to work on (+2%/heat).</summary>
    public static int HackDuration(TargetDef t, GameState s)
        => Math.Max(1, (int)Math.Round(
               t.DurationTicks
               * Math.Pow(0.95, s.UpgradeLevels.GetValueOrDefault("cpu"))
               * Math.Pow(0.97, s.PerkLevels.GetValueOrDefault("overclock"))
               * PrestigeSystem.SpeedMult(s)
               * (1 + StateFor(s, t).Heat / 50)));

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
        } * (1 + 0.1 * s.PerkLevels.GetValueOrDefault("datamine"))
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
                double steal = StealBase * ts.CurrentMoney
                    * (1 + 0.1 * s.PerkLevels.GetValueOrDefault("ransom"))
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
                    : Flavor.Fill(flavor, target: h.TargetName,
                                  money: steal, xp: h.XpReward));

                if (ts.CurrentMoney < TappedOutPct * max)
                    s.Log($"{h.TargetName} is tapped out — pool at ${ts.CurrentMoney:0.#}. Grow it back or move on.");
                break;
        }
    }
}

public static class EconomySystem
{
    /// <summary>Botnet passive income per tick: 0.2 * lvl * 1.15^lvl.</summary>
    public static double IncomePerTick(GameState s)
    {
        int lvl = s.UpgradeLevels.GetValueOrDefault("botnet");
        return lvl == 0 ? 0
            : 0.2 * lvl * Math.Pow(1.15, lvl)
              * (1 + 0.1 * s.PerkLevels.GetValueOrDefault("scavenger"));
    }

    public static void Tick(GameState s)
    {
        double income = IncomePerTick(s);
        if (income > 0)
        {
            s.Money += income;
            s.Stats.MoneyEarned += income;
        }
    }
}

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

/// <summary>
/// Ambient world flavor: occasionally logs an idle line so the terminal
/// feels alive even when the player is idle. ~1 line per 3 minutes at 10 Hz.
/// </summary>
public static class AtmosphereSystem
{
    private const double IdleChancePerTick = 0.0005;
    private static readonly Random Rng = new();

    public static void Tick(GameState s, GameContent c)
    {
        if (c.Flavor.Idle.Count == 0)
            return;
        if (Rng.NextDouble() < IdleChancePerTick)
            s.Log(Flavor.Fill(Flavor.Pick(c.Flavor.Idle)));
    }
}

// ---------------------------------------------------------------------------
// Achievements: content-defined goals checked against lifetime stats, once
// per tick. Because stats update inside Tick, achievements unlock correctly
// during offline catch-up too. Each achievement is awarded exactly once.
// ---------------------------------------------------------------------------

public static class AchievementsSystem
{
    private const string TargetHackPrefix = "targetHacks:";

    public static double StatValue(GameState s, string key)
    {
        if (key.StartsWith(TargetHackPrefix))
            return s.Stats.HacksPerTarget.GetValueOrDefault(key[TargetHackPrefix.Length..]);
        return key switch
        {
            "hacks" => s.Stats.TotalHacks,
            "moneyEarned" => s.Stats.MoneyEarned,
            "level" => s.Level,
            "upgradesBought" => s.Stats.UpgradesBought,
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

// ---------------------------------------------------------------------------
// Contracts: timed bonus objectives. Pure bonus — missing the deadline only
// loses the reward. Offers rotate from the content pool (max 3 at a time,
// never the same contract offered and active simultaneously); completed
// contracts can be offered again later.
//
// Progress is a stat DELTA since acceptance (baseline snapshotted at accept),
// so goals like "complete 5 hacks" work regardless of lifetime totals, and it
// reuses the same stat keys as achievements.
// ---------------------------------------------------------------------------

public static class ContractsSystem
{
    public const int MaxOffers = 3;

    private static readonly Random Rng = new();

    public static double Progress(GameState s, GameContent c, ActiveContractState a)
    {
        var def = c.FindContract(a.DefId);
        return def is null ? 0
            : Math.Max(0, AchievementsSystem.StatValue(s, def.Stat) - a.StartStat);
    }

    public static void Accept(GameState s, GameContent c, string defId)
    {
        var def = c.FindContract(defId);
        var offer = s.ContractOffers.Find(o => o.DefId == defId);
        if (def is null || offer is null)
        {
            s.Log($"No open contract '{defId}' (see 'contracts').");
            return;
        }

        s.ContractOffers.Remove(offer);
        s.ActiveContracts.Add(new ActiveContractState
        {
            DefId = def.Id,
            DeadlineRemaining = def.DurationTicks,
            StartStat = AchievementsSystem.StatValue(s, def.Stat),
        });
        s.Log($"Contract accepted: {def.Name}. Deadline: {def.DurationTicks} ticks.");
    }

    public static void Tick(GameState s, GameContent c)
    {
        // Refill the offer board.
        while (s.ContractOffers.Count < MaxOffers)
        {
            var pool = c.Contracts
                .Where(d => s.ContractOffers.All(o => o.DefId != d.Id)
                         && s.ActiveContracts.All(a => a.DefId != d.Id))
                .ToList();
            if (pool.Count == 0)
                break;

            var pick = pool[Rng.Next(pool.Count)];
            s.ContractOffers.Add(new ContractOfferState { DefId = pick.Id });
            s.Log($"New contract available: {pick.Name} (see 'contracts').");
        }

        // Advance active contracts: completion check, then deadline.
        for (int i = s.ActiveContracts.Count - 1; i >= 0; i--)
        {
            var a = s.ActiveContracts[i];
            var def = c.FindContract(a.DefId);
            if (def is null)
            {
                s.ActiveContracts.RemoveAt(i); // def removed from content
                continue;
            }

            if (Progress(s, c, a) >= def.Goal)
            {
                s.Money += def.RewardMoney;
                s.Xp += def.RewardXp;
                s.Log($"CONTRACT COMPLETE: {def.Name} — bonus +${def.RewardMoney:0.##}, +{def.RewardXp:0.#} XP.");
                s.ActiveContracts.RemoveAt(i);
                continue;
            }

            a.DeadlineRemaining--;
            if (a.DeadlineRemaining <= 0)
            {
                s.Log($"Contract failed: {def.Name} — expired. No bonus this time.");
                s.ActiveContracts.RemoveAt(i);
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Prestige: burn your identity at level >= UnlockLevel for 0-day tokens
// that scale with your level at burn time. Tokens install implants
// (permanent upgrades that survive every future reset).
//
// Reset wipes: money, XP, level, skill points, rig, perks, contracts.
// Keeps: 0-days, implants, achievements, lifetime stats.
//
// The unlock is a THRESHOLD (learn the mechanic once), but the trigger is
// manual + scaling — every burn is a push-your-luck decision, which is the
// actual gameplay.
// ---------------------------------------------------------------------------

public static class PrestigeSystem
{
    public const int UnlockLevel = 10;

    public static bool Unlocked(GameState s) => s.Level >= UnlockLevel;

    /// <summary>0-days for burning at 'level': 1 at 10, 2 at 12, 4 at 14, ...</summary>
    public static long TokensForLevel(int level)
        => level < UnlockLevel
            ? 0
            : (long)Math.Floor(Math.Pow(2, (level - UnlockLevel) / 2.0));

    // -- Implant effect helpers (referenced by id in the content JSON) --

    public static double MoneyMult(GameState s)
        => 1 + 0.1 * s.PrestigeLevels.GetValueOrDefault("notoriety");

    public static double XpMult(GameState s)
        => 1 + 0.1 * s.PrestigeLevels.GetValueOrDefault("blackice");

    public static double SpeedMult(GameState s)
        => Math.Pow(0.97, s.PrestigeLevels.GetValueOrDefault("neural"));

    public static double SeedMoney(GameState s)
        => 250 * Math.Pow(3, s.PrestigeLevels.GetValueOrDefault("burner"));

    /// <summary>Execute the burn. Validates; no-ops below the unlock level.</summary>
    public static void Reset(GameState s, GameContent c)
    {
        if (!Unlocked(s))
        {
            s.Log($"Identity burning requires level {UnlockLevel}.");
            return;
        }

        long tokens = TokensForLevel(s.Level);
        if (tokens <= 0)
        {
            s.Log("Not enough progress to burn for 0-days.");
            return;
        }

        s.ZeroDays += tokens;
        s.Burns++;

        // Wipe the run (keep: stats, achievements, 0-days, implants).
        s.Money = SeedMoney(s);
        s.Xp = 0;
        s.Level = 1;
        s.SkillPoints = 0;
        s.UpgradeLevels = new();
        s.PerkLevels = new();
        s.ActiveJobs = new();
        s.Targets = new();   // fresh identity: all marks cold and fully stocked
        s.ContractOffers = new();
        s.ActiveContracts = new();

        s.Log($"Identity burned. +{tokens} 0-days. The dark web forgets you... for now.");
    }
}