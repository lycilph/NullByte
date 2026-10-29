using NullByte.ConsoleUI.Systems;
using NullByte.Core;
using NullByte.Core.Commands;
using NullByte.Core.Systems;

namespace NullByte.ConsoleUI;

internal class Program
{
    private static readonly object ConsoleLock = new();

    static async Task Main()
    {
        Console.WriteLine("=== NULLBYTE ===");
        Console.WriteLine("A hack, a tick, a dollar.\n");

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
        int nextEvent = 0;
        var printer = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(500, cts.Token);
                var log = session.State.EventLog;
                if (nextEvent > log.Count)
                    nextEvent = log.Count; // log was trimmed
                lock (ConsoleLock)
                {
                    while (nextEvent < log.Count)
                        Console.WriteLine("  " + log[nextEvent++]);
                }
            }
        });

        PrintHelp();

        // -- Input loop (main thread) --
        bool running = true;
        while (running)
        {
            Console.Write("> ");
            string? line = Console.ReadLine();
            if (line is null)
                break;

            var args = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0)
                continue;

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

    private static void PrintHelp()
    {
        Console.WriteLine("Commands: hack <id> | buy <id> | targets | upgrades | status | save | quit | help");
        Console.WriteLine("Start here: 'targets' then 'hack webmail'\n");
    }

    private static void PrintTargets(GameSession session)
    {
        Console.WriteLine("TARGETS");
        foreach (var t in session.Content.Targets)
        {
            string status = session.State.Level >= t.MinLevel ? "OK" : $"lvl {t.MinLevel}";
            Console.WriteLine($"  {t.Id,-12} {t.Name,-22} ${t.MoneyReward,-8:0.#}  {t.XpReward,5:0.#} xp  {t.DurationTicks} ticks  [{status}]");
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

    private static void PrintStatus(GameSession session)
    {
        var s = session.State;
        double income = EconomySystem.IncomePerTick(s);
        double xpInto = s.Xp - ProgressionSystem.TotalXpForLevel(s.Level);
        double xpNeed = ProgressionSystem.XpForLevelUp(s.Level);

        Console.WriteLine($"== STATUS  t{s.GameTick} ==");
        Console.WriteLine($"  Money: ${s.Money:0.##}   (+${income:0.###}/tick)");
        Console.WriteLine($"  Level: {s.Level}   XP: {xpInto:0.#} / {xpNeed:0.#}");
        Console.WriteLine($"  Hack slots: {s.ActiveHacks.Count}/{HackSystem.MaxConcurrentHacks(s)}");

        if (s.ActiveHacks.Count > 0)
        {
            foreach (var h in s.ActiveHacks)
                Console.WriteLine($"    {h.TargetName}: {h.TicksRemaining}/{h.TotalTicks} ticks left");
        }
    }
}