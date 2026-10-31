using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nullbyte.Core;
using Nullbyte.Scripting;

namespace Nullbyte.Console;

public static class Program
{
    private static readonly object ConsoleLock = new();

    public static async Task Main()
    {
        RunBootSequence();

        var content = ContentLoader.Load();

        // -- Load save + offline catch-up --
        GameSession session;
        var loaded = SaveSystem.Load();
        if (loaded is { } l)
        {
            session = new GameSession(content, l.State);
            long offlineTicks = (long)(l.OfflineElapsed.TotalMilliseconds / GameSession.TickMs);
            if (offlineTicks > 0)
            {
                session.FastForward(offlineTicks);
                Console.WriteLine($"Save loaded. Away for {l.OfflineElapsed:hh\\:mm\\:ss} — advanced {offlineTicks} ticks.");
                Console.WriteLine($"Welcome back. Money: ${session.State.Money:0.##}\n");
            }
        }
        else
        {
            session = new GameSession(content);
        }

        // -- Scripting: Roslyn runtime + restart persisted scripts --
        var scripting = new ScriptingSystem(session);
        var loadTasks = new List<Task>();
        foreach (var entry in ScriptPersistence.Load())
        {
            if (!entry.Running)
                continue;
            // Compiles can be slow (Roslyn warm-up); do them before the
            // banner, then the loop ticks them like any other script.
            loadTasks.Add(scripting.StartAsync(entry.Name, entry.Source, entry.Data));
        }
        if (loadTasks.Count > 0)
        {
            await Task.WhenAll(loadTasks);
            Console.WriteLine($"Restarted {loadTasks.Count} script(s): {string.Join(", ", scripting.Scripts.Select(s => s.Name))}");
        }

        // -- The game loop: 100 ms fixed ticks on a background task --
        var cts = new CancellationTokenSource();
        var loop = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    session.Tick();
                    scripting.Tick();   // releases the script barrier, once per tick
                    await Task.Delay(GameSession.TickMs, cts.Token);
                }
            }
            catch (TaskCanceledException) { /* shutting down */ }
        });

        // -- Event printer: drains the EventLog twice a second --
        // Consumes by TOTALS, not list positions: the log is a trimmed
        // window, so a list index goes stale once entries start dropping
        // off the front. Mapping: entry i lives at log[i - (total - Count)].
        long nextEvent = 0;
        int handledBurns = session.State.Burns;
        var scriptCursors = new Dictionary<string, long>();
        var printer = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(500, cts.Token);

                // -- Burn detection (cursor pattern): a new identity gets a
                // fresh boot. Skip the stale log backlog — the boot animation
                // IS the announcement; pre-burn events don't matter anymore.
                if (session.State.Burns != handledBurns)
                {
                    handledBurns = session.State.Burns;
                    lock (ConsoleLock)
                    {
                        RunBootSequence();
                        nextEvent = session.State.EventsLogged;
                    }
                }

                var log = session.State.EventLog;
                long total = session.State.EventsLogged;
                long firstKept = total - log.Count;      // absolute index of log[0]
                long from = Math.Max(nextEvent, firstKept);

                lock (ConsoleLock)
                {
                    for (long i = from; i < total; i++)
                        Console.WriteLine("  " + log[(int)(i - firstKept)]);
                }
                nextEvent = total;

         
                // -- Script output (same cursor pattern, per script) --
                // First sight of a script: skip its backlog, only live lines.
                foreach (var script in scripting.Scripts)
                {
                    if (!scriptCursors.TryGetValue(script.Name, out long seen))
                    {
                        scriptCursors[script.Name] = script.OutputTotal;
                        continue;
                    }

                    long sTotal = script.OutputTotal;
                    long sFirst = sTotal - script.Output.Count;
                    long sFrom = Math.Max(seen, sFirst);

                    lock (ConsoleLock)
                    {
                        for (long i = sFrom; i < sTotal; i++)
                            Console.WriteLine("  " + script.Output[(int)(i - sFirst)]);
                    }
                    scriptCursors[script.Name] = sTotal;
                }
            }
        });

        PrintHelp(content);

        // -- Input loop (main thread) --
        bool running = true;
        while (running)
        {
            Console.Write("nullbyte@rig:~$ ");
            string? line = Console.ReadLine();
            if (line is null)
                break;

            var args = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0)
                continue;

            // 'run' compiles asynchronously (Roslyn can take a second); the
            // compile happens OUTSIDE ConsoleLock so the UI stays live. Every
            // other command is synchronous and lock-protected.
            Task? pendingRun = null;

            lock (ConsoleLock)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "hack":
                        if (args.Length < 2)
                            Console.WriteLine("Usage: hack <target-id>  (see 'targets')");
                        else
                            session.Submit(new StartHackCommand(args[1]));
                        break;

                    case "buy":
                        if (args.Length < 2)
                            Console.WriteLine("Usage: buy <upgrade-id>  (see 'upgrades')");
                        else
                            session.Submit(new BuyUpgradeCommand(args[1]));
                        break;

                    case "targets":
                        PrintTargets(session);
                        break;

                    case "upgrades":
                        PrintUpgrades(session);
                        break;

                    case "status":
                        PrintStatus(session);
                        break;

                    case "stats":
                        PrintStats(session);
                        break;

                    case "achievements":
                        PrintAchievements(session);
                        break;

                    case "contracts":
                        PrintContracts(session);
                        break;

                    case "accept":
                        if (args.Length < 2)
                            Console.WriteLine("Usage: accept <contract-id>  (see 'contracts')");
                        else
                            session.Submit(new AcceptContractCommand(args[1]));
                        break;

                    case "perks":
                        PrintPerks(session);
                        break;

                    case "skill":
                        if (args.Length < 2)
                            Console.WriteLine("Usage: skill <perk-id>  (see 'perks')");
                        else
                            session.Submit(new SpendSkillCommand(args[1]));
                        break;

                    case "prestige":
                        if (args.Length >= 2 && args[1] == "confirm")
                            session.Submit(new PrestigeCommand());
                        else
                            PrintPrestige(session);
                        break;

                    case "implants":
                        PrintImplants(session);
                        break;

                    case "implant":
                        if (args.Length < 2)
                            Console.WriteLine("Usage: implant <id>  (see 'implants')");
                        else
                            session.Submit(new ImplantCommand(args[1]));
                        break;

                    case "run":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: run <script-name>  (runs scripts/<name>.cs)");
                        }
                        else if (scripting.Find(args[1])?.IsRunning == true)
                        {
                            Console.WriteLine($"'{args[1]}' is already running. 'stop {args[1]}' first.");
                        }
                        else if (!File.Exists(ScriptPersistence.SourcePath(args[1])))
                        {
                            Console.WriteLine($"No file at {ScriptPersistence.SourcePath(args[1])}");
                        }
                        else
                        {
                            // Retire any stopped/errored instance with this name
                            // so the list doesn't accumulate duplicates.
                            scripting.Stop(args[1]);
                            pendingRun = StartScriptAsync(scripting, args[1]);
                        }
                        break;

                    case "stop":
                        if (args.Length < 2)
                            Console.WriteLine("Usage: stop <script-name>");
                        else
                            scripting.Stop(args[1]);
                        break;

                    case "scripts":
                        if (scripting.Scripts.Count == 0)
                        {
                            Console.WriteLine("No scripts. Write scripts/<name>.cs, then 'run <name>'.");
                            break;
                        }
                        foreach (var s in scripting.Scripts)
                        {
                            string status = s.IsRunning ? "RUNNING"
                                          : s.Error is not null ? "ERROR"
                                          : "STOPPED";
                            Console.WriteLine($"  {s.Name,-16} [{status}]");
                        }
                        break;

                    case "save":
                        SaveSystem.Save(session.State);
                        ScriptPersistence.Save(scripting.Scripts);
                        Console.WriteLine("Saved.");
                        break;

                    case "help":
                        PrintHelp(content);
                        break;

                    case "quit":
                    case "exit":
                        running = false;
                        break;

                    default:
                        Console.WriteLine($"Unknown command '{args[0]}'. Try 'help'.");
                        break;
                }
            }

            // Await the 'run' outside the lock so the compile doesn't freeze
            // the printer/loop tasks.
            if (pendingRun is not null)
                await pendingRun;
        }

        cts.Cancel();
        try { await Task.WhenAll(loop, printer); }
        catch (Exception) { /* tasks end via cancellation at shutdown */ }
        SaveSystem.Save(session.State);
        ScriptPersistence.Save(scripting.Scripts);
        Console.WriteLine("Saved. Bye.");
    }

    /// <summary>
    /// Compiles + starts a script from scripts/<name>.cs. Runs outside
    /// ConsoleLock (Roslyn compile takes real time) and re-acquires the lock
    /// only to report the outcome.
    /// </summary>
    private static async Task StartScriptAsync(ScriptingSystem scripting, string name)
    {
        try
        {
            await scripting.StartAsync(name, File.ReadAllText(ScriptPersistence.SourcePath(name)));
            lock (ConsoleLock) { Console.WriteLine($"'{name}' running."); }
        }
        catch (ScriptCompileException ex)
        {
            lock (ConsoleLock) { Console.WriteLine("COMPILE ERROR:\n" + ex.Message); }
        }
        catch (Exception ex)
        {
            lock (ConsoleLock) { Console.WriteLine($"Can't start '{name}': {ex.Message}"); }
        }
    }

    private static readonly Random BootRng = new();

    /// <summary>
    /// Full boot: clear screen, banner, animated lines. Run at startup and
    /// again after an identity burn — a fresh identity gets a fresh boot.
    /// </summary>
    private static void RunBootSequence()
    {
        Console.Clear();
        Console.WriteLine(@"
 _   _ ____  _   _  ___  _   _ _____ _____   ___   ___ ___
| \ | |  _ \| | | |/ _ \| | | |_   _| ____| / _ \ / __|__ \
|  \| | |_) | | | | | | | | | | | | |  _|  | | | | (__| ) |
| |\  |  _ <| |_| | |_| | |_| | | | | |___ | |_| | (__ / /
|_| \_|_| \_\\___/  \___/ \___/  |_| |_____| \___/ \___|_|
");
        BootLine("booting rig", 16);
        BootLine("establishing tunnel", 16);
        BootLine("wiping fingerprints", 17);
        BootLine("loading contacts", 16);
        Console.WriteLine();
    }

    /// <summary>
    /// Types a boot line dot by dot with a random delay per dot, so the rig
    /// feels like it's actually churning. Occasional longer pauses make it
    /// feel organic rather than metronomic.
    /// </summary>
    private static void BootLine(string label, int dotCount)
    {
        Console.Write($"{label} ");
        for (int i = 0; i < dotCount; i++)
        {
            Console.Write('.');
            int delay = BootRng.Next(20, 80);
            if (BootRng.Next(10) == 0)
                delay += BootRng.Next(150, 400); // rare hiccup
            Thread.Sleep(delay);
        }
        Thread.Sleep(BootRng.Next(60, 180));      // beat before OK
        Console.Write(" [ OK ]\n");
    }

    private static void PrintAchievements(GameSession session)
    {
        var s = session.State;
        Console.WriteLine($"ACHIEVEMENTS — {s.Achievements.Count}/{session.Content.Achievements.Count} earned");
        foreach (var a in session.Content.Achievements)
        {
            string mark = s.Achievements.Contains(a.Id) ? "[x]" : "[ ]";
            Console.WriteLine($"  {mark} {a.Name,-20} {a.Description}");
        }
    }

    private static void PrintStats(GameSession session)
    {
        var s = session.State;
        var time = TimeSpan.FromMilliseconds(s.GameTick * GameSession.TickMs);

        Console.WriteLine($"== STATS ==");
        Console.WriteLine($"  Time played:   {time:hh\\:mm\\:ss}  ({s.GameTick} ticks)");
        Console.WriteLine($"  Hacks done:    {s.Stats.TotalHacks}");
        Console.WriteLine($"  Money earned:  ${s.Stats.MoneyEarned:0.##} (current: ${s.Money:0.##})");
        Console.WriteLine($"  Upgrades:      {s.Stats.UpgradesBought} bought");
        Console.WriteLine($"  Perks:         {s.PerkLevels.Values.Sum()} levels trained");

        var favorites = s.Stats.HacksPerTarget.OrderByDescending(kv => kv.Value).Take(3).ToList();
        if (favorites.Count > 0)
            Console.WriteLine($"  Favorite marks: {string.Join(", ", favorites.Select(kv => $"{kv.Key} x{kv.Value}"))}");
    }

    private static void PrintContracts(GameSession session)
    {
        var s = session.State;
        Console.WriteLine("CONTRACTS — timed bonus objectives; missing a deadline just loses the bonus");

        if (s.ActiveContracts.Count > 0)
        {
            Console.WriteLine("  Active:");
            foreach (var a in s.ActiveContracts)
            {
                var def = session.Content.FindContract(a.DefId);
                if (def is null) continue;
                double progress = ContractsSystem.Progress(s, session.Content, a);
                Console.WriteLine($"    {def.Name,-16} {progress:0.#}/{def.Goal:0.#} — {a.DeadlineRemaining} ticks left  (+${def.RewardMoney:0.##}, +{def.RewardXp:0.#} xp)");
            }
        }

        if (s.ContractOffers.Count > 0)
        {
            Console.WriteLine("  Open offers:");
            foreach (var o in s.ContractOffers)
            {
                var def = session.Content.FindContract(o.DefId);
                if (def is null) continue;
                Console.WriteLine($"    {def.Id,-16} {def.Name,-16} {def.Description,-28} (+${def.RewardMoney:0.##}, +{def.RewardXp:0.#} xp, {def.DurationTicks} ticks)");
            }
            Console.WriteLine("  Accept with: accept <id>");
        }
    }

    private static void PrintPrestige(GameSession session)
    {
        var s = session.State;
        Console.WriteLine("== PRESTIGE ==");
        Console.WriteLine($"  0-days: {s.ZeroDays}");

        if (!PrestigeSystem.Unlocked(s))
        {
            Console.WriteLine($"  Identity burning unlocks at level {PrestigeSystem.UnlockLevel} (you are level {s.Level}).");
            return;
        }

        long tokens = PrestigeSystem.TokensForLevel(s.Level);
        Console.WriteLine($"  Burn your identity NOW for +{tokens} 0-days.");
        Console.WriteLine("  Wipes: money, XP, level, skill points, rig, perks, contracts.");
        Console.WriteLine("  Keeps: 0-days, implants, achievements, lifetime stats.");
        Console.WriteLine("  Confirm with: prestige confirm   |   Spend tokens: 'implants'");
    }

    private static void PrintImplants(GameSession session)
    {
        var s = session.State;
        Console.WriteLine($"IMPLANTS — 0-days available: {s.ZeroDays}");
        foreach (var i in session.Content.Implants)
        {
            int owned = s.PrestigeLevels.GetValueOrDefault(i.Id);
            long cost = (long)Math.Ceiling(i.BaseCost * Math.Pow(i.GrowthRate, owned));
            string maxed = owned >= i.MaxLevel ? " MAX" : $" next: {cost} 0-days";
            Console.WriteLine($"  {i.Id,-11} {i.Name,-20} lvl {owned,2}/{i.MaxLevel,-2} {maxed,-16} {i.Description}");
        }
        Console.WriteLine("  Install with: implant <id>   (survive identity burns)");
    }

    private static void PrintHelp(GameContent content)
    {
        Console.WriteLine("Commands: hack <id> | buy <id> | skill <id> | accept <id> | implant <id> | prestige [confirm] | run <name> | stop <name> | scripts | targets | upgrades | perks | contracts | implants | achievements | stats | status | save | quit | help");
        Console.WriteLine("Scripts: write scripts/<name>.cs (C#, globals expose 'ns'), then 'run <name>' — e.g. await ns.Hack(\"webmail\");");
        Console.WriteLine("Start here: 'targets' then 'hack webmail'\n");
    }

    private static void PrintTargets(GameSession session)
    {
        Console.WriteLine("TARGETS");
        var s = session.State;
        int cpu = s.UpgradeLevels.GetValueOrDefault("cpu");
        int ram = s.UpgradeLevels.GetValueOrDefault("ram");

        foreach (var t in session.Content.Targets)
        {
            string status;
            if (s.Level >= t.MinLevel && cpu >= t.RequiredCpu && ram >= t.RequiredRam)
            {
                status = "OK";
            }
            else
            {
                var req = new List<string>();
                if (s.Level < t.MinLevel) req.Add($"lvl {t.MinLevel}");
                if (cpu < t.RequiredCpu) req.Add($"cpu {t.RequiredCpu}");
                if (ram < t.RequiredRam) req.Add($"ram {t.RequiredRam}");
                status = "needs " + string.Join(", ", req);
            }
            Console.WriteLine($"  {t.Id,-14} {t.Name,-22} ${t.MoneyReward,-9:0.#}  {t.XpReward,5:0.#} xp  {t.DurationTicks,3} ticks  [{status}]");
        }
    }

    private static void PrintUpgrades(GameSession session)
    {
        Console.WriteLine("UPGRADES");
        foreach (var u in session.Content.Upgrades)
        {
            int owned = session.State.UpgradeLevels.GetValueOrDefault(u.Id);
            double cost = CostMath.UpgradeCost(u, owned);
            string maxed = owned >= u.MaxLevel ? " MAX" : $" next: ${cost:0.##}";
            Console.WriteLine($"  {u.Id,-8} {u.Name,-14} lvl {owned,2}/{u.MaxLevel,-2} {maxed,-18} {u.Description}");
        }
    }

    private static void PrintPerks(GameSession session)
    {
        Console.WriteLine($"SKILL TREE — points available: {session.State.SkillPoints}");
        foreach (var p in session.Content.Perks)
        {
            int owned = session.State.PerkLevels.GetValueOrDefault(p.Id);
            Console.WriteLine($"  {p.Id,-11} {p.Name,-14} lvl {owned,2}/{p.MaxLevel,-2}  {p.Description}");
        }
        Console.WriteLine("Spend with: skill <id>");
    }

    private static void PrintStatus(GameSession session)
    {
        var s = session.State;
        double income = EconomySystem.IncomePerTick(s);
        double xpInto = s.Xp - ProgressionSystem.TotalXpForLevel(s.Level);
        double xpNeed = ProgressionSystem.XpForLevelUp(s.Level);

        Console.WriteLine($"== STATUS  t{s.GameTick} ==");
        Console.WriteLine($"  Money: ${s.Money:0.##}   (+${income:0.###}/tick)");
        Console.WriteLine($"  Level: {s.Level}   XP: {xpInto:0.#} / {xpNeed:0.#}   Skill points: {s.SkillPoints}");
        Console.WriteLine($"  Hack slots: {s.ActiveHacks.Count}/{HackSystem.MaxConcurrentHacks(s)}");
        Console.WriteLine($"  Rig: cpu {s.UpgradeLevels.GetValueOrDefault(\"cpu\")} | ram {s.UpgradeLevels.GetValueOrDefault(\"ram\")} | botnet {s.UpgradeLevels.GetValueOrDefault(\"botnet\")}");

        if (s.ActiveHacks.Count > 0)
        {
            foreach (var h in s.ActiveHacks)
                Console.WriteLine($"    {h.TargetName}: {h.TicksRemaining}/{h.TotalTicks} ticks left");
        }
    }
}