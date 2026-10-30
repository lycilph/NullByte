using NullByte.ConsoleUI.Systems;
using NullByte.Core;
using NullByte.Core.Commands;
using NullByte.Core.Systems;

namespace NullByte.ConsoleUI;

internal class Program
{
    private static readonly Random bootRng = new();
    private static readonly object consoleLock = new();

    static async Task Main()
    {
        Console.WriteLine("=== NULLBYTE ===");
        Console.WriteLine(@"
      ___           ___           ___       ___       ___           ___           ___           ___     
     /\__\         /\__\         /\__\     /\__\     /\  \         |\__\         /\  \         /\  \    
    /::|  |       /:/  /        /:/  /    /:/  /    /::\  \        |:|  |        \:\  \       /::\  \   
   /:|:|  |      /:/  /        /:/  /    /:/  /    /:/\:\  \       |:|  |         \:\  \     /:/\:\  \  
  /:/|:|  |__   /:/  /  ___   /:/  /    /:/  /    /::\~\:\__\      |:|__|__       /::\  \   /::\~\:\  \ 
 /:/ |:| /\__\ /:/__/  /\__\ /:/__/    /:/__/    /:/\:\ \:|__|     /::::\__\     /:/\:\__\ /:/\:\ \:\__\
 \/__|:|/:/  / \:\  \ /:/  / \:\  \    \:\  \    \:\~\:\/:/  /    /:/~~/~       /:/  \/__/ \:\~\:\ \/__/
     |:/:/  /   \:\  /:/  /   \:\  \    \:\  \    \:\ \::/  /    /:/  /        /:/  /       \:\ \:\__\  
     |::/  /     \:\/:/  /     \:\  \    \:\  \    \:\/:/  /     \/__/         \/__/         \:\ \/__/  
     /:/  /       \::/  /       \:\__\    \:\__\    \::/__/                                   \:\__\    
     \/__/         \/__/         \/__/     \/__/     ~~                                        \/__/\n");
        BootLine("booting rig", 21);
        BootLine("establishing tunnel", 13);
        BootLine("wiping fingerprints", 13);
        BootLine("loading contacts", 16);
        Console.WriteLine();

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

        // -- The game loop: 100 ms fixed ticks on a background task --
        var cts = new CancellationTokenSource();
        var loop = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    session.Tick();
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
        var printer = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(500, cts.Token);
                var log = session.State.EventLog;
                long total = session.State.EventsLogged;
                long firstKept = total - log.Count;      // absolute index of log[0]
                long from = Math.Max(nextEvent, firstKept);

                lock (consoleLock)
                {
                    for (long i = from; i < total; i++)
                        Console.WriteLine("  " + log[(int)(i - firstKept)]);
                }
                nextEvent = total;
            }
        });

        PrintHelp();

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

            lock (consoleLock)
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

                    case "perks":
                        PrintPerks(session);
                        break;

                    case "skill":
                        if (args.Length < 2)
                            Console.WriteLine("Usage: skill <perk-id>  (see 'perks')");
                        else
                            session.Submit(new SpendSkillCommand(args[1]));
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

                    case "save":
                        SaveSystem.Save(session.State);
                        Console.WriteLine("Saved.");
                        break;

                    case "help":
                        PrintHelp();
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
        }

        cts.Cancel();
        try
        {
            loop.Wait();
            printer.Wait();
        }
        catch (AggregateException) { }
        SaveSystem.Save(session.State);

        Console.WriteLine("Saved. Bye.");
        await Task.Delay(500);
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
            int delay = bootRng.Next(20, 80);
            if (bootRng.Next(10) == 0)
                delay += bootRng.Next(150, 400); // rare hiccup
            Thread.Sleep(delay);
        }
        Thread.Sleep(bootRng.Next(60, 180));      // beat before OK
        Console.Write(" [ OK ]\n");
    }

    private static void PrintAchievements(GameSession session)
    {
        var s = session.State;
        var c = session.Content;
        Console.WriteLine($"ACHIEVEMENTS — {s.Achievements.Count}/{c.Achievements.Count} earned");
        foreach (var a in c.Achievements)
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
        Console.WriteLine($"  Contracts:     {s.Stats.ContractsCompleted} completed / {s.Stats.ContractsFailed} failed");

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

    private static void PrintHelp()
    {
        Console.WriteLine("Commands: hack <id> | buy <id> | skill <id> | accept <id> | targets | upgrades | perks | contracts | achievements | stats | status | save | quit | help");
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
        Console.WriteLine($"  Rig: cpu {s.UpgradeLevels.GetValueOrDefault("cpu")} | ram {s.UpgradeLevels.GetValueOrDefault("ram")} | botnet {s.UpgradeLevels.GetValueOrDefault("botnet")}");

        if (s.ActiveHacks.Count > 0)
        {
            foreach (var h in s.ActiveHacks)
                Console.WriteLine($"    {h.TargetName}: {h.TicksRemaining}/{h.TotalTicks} ticks left");
        }
    }
}