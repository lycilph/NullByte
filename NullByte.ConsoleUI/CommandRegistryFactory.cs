using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Simulation;
using System.Numerics;

namespace NullByte.ConsoleUI;

public static class CommandRegistryFactory
{
    public static CommandRegistry Create()
    {
        var registry = new CommandRegistry();

        RegisterDebugCommands(registry);
        RegisterGameCommands(registry);

        return registry;
    }

    private static void RegisterDebugCommands(CommandRegistry registry)
    {
        registry.Register(
            new CommandDefinition(
                "debug",
                "debug",
                "Debug commands.",
                DebugAsync)
            { Debug = true });

        registry.Register(
            new CommandDefinition(
                "time",
                "time",
                "Show current game time.",
                TimeAsync)
            { Debug = true });

        registry.Register(
            new CommandDefinition(
                "thread",
                "thread",
                "Show the current game thread.",
                ThreadAsync)
            { Debug = true });

        registry.Register(
            new CommandDefinition(
                "demo",
                "demo",
                "Run a scheduler demonstration.",
                RunDemoAsync)
            { Debug = true });
    }

    private static void RegisterGameCommands(CommandRegistry registry)
    {
        registry.Register(
            new CommandDefinition(
                "help",
                "help",
                "Show this help.",
                HelpAsync));

        registry.Register(
            new CommandDefinition(
                "money",
                "money",
                "Show the player's money.",
                MoneyAsync));

        registry.Register(
            new CommandDefinition(
                "status",
                "status",
                "Show player status.",
                StatusAsync));

        registry.Register(
            new CommandDefinition(
                "ls",
                "ls",
                "List files.",
                ListFilesAsync));

        registry.Register(
            new CommandDefinition(
                "scan",
                "scan",
                "Scan connected servers.",
                ScanAsync));

        registry.Register(
            new CommandDefinition(
                "analyze",
                "analyze [hostname]",
                "Show server information.",
                AnalyzeAsync)
            { ArgumentCompletion = ArgumentCompletionKind.KnownServer });

        registry.Register(
            new CommandDefinition(
                "connect",
                "connect <hostname>",
                "Connect to a discovered server.",
                ConnectAsync)
            { ArgumentCompletion = ArgumentCompletionKind.ConnectableServer });

        registry.Register(
            new CommandDefinition(
                "home",
                "home",
                "Return to your home server.",
                HomeAsync));

        registry.Register(
            new CommandDefinition(
                "nuke",
                "nuke",
                "Gain root access to current server.",
                NukeAsync));

        registry.Register(
            new CommandDefinition(
                "hack",
                "hack",
                "Attempt to steal money.",
                HackAsync));

        registry.Register(
            new CommandDefinition(
                "grow",
                "grow",
                "Increase server money.",
                GrowAsync));

        registry.Register(
            new CommandDefinition(
                "weaken",
                "weaken",
                "Reduce server security.",
                WeakenAsync));

        registry.Register(
            new CommandDefinition(
                "clear",
                "clear",
                "Clear the terminal.",
                ClearAsync,
                "cls"));
    }

    // Register debug commands

    private static Task DebugAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: debug");
            return Task.CompletedTask;
        }

        context.Terminal.WriteLine("Debugging commands:");
        foreach (var command in context.CommandRegistry.Commands.Where(c => c.Debug))
            context.Terminal.WriteLine($"  {command.Usage,-22} {command.Description}");

        return Task.CompletedTask;
    }
    
    private static Task TimeAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: time");
            return Task.CompletedTask;
        }

        var clock = context.GameLoop.Clock;
        context.Terminal.WriteLine($"Game time: {clock.Elapsed.TotalSeconds:F3}s");

        return Task.CompletedTask;
    }

    private static Task ThreadAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: thread");
            return Task.CompletedTask;
        }

        context.Terminal.WriteLine($"Thread ID: {Environment.CurrentManagedThreadId}");
        context.Terminal.WriteLine($"Is game thread: {context.GameLoop.IsGameThread}");

        return Task.CompletedTask;
    }

    private static async Task RunDemoAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: demo");
            return;
        }

        var terminal = context.Terminal;
        try
        {
            terminal.WriteLine("[M1] Starting scheduler demonstration.");
            terminal.WriteLine($"[M1] Game thread ID: {Environment.CurrentManagedThreadId}");
            terminal.WriteLine();

            var slowOperation =
                DemonstrateOperationAsync(
                    name: "operation-a",
                    duration: TimeSpan.FromSeconds(3),
                    context.GameLoop,
                    terminal);

            var fastOperation =
                DemonstrateOperationAsync(
                    name: "operation-b",
                    duration: TimeSpan.FromSeconds(1.5),
                    context.GameLoop,
                    terminal);

            await Task.WhenAll(slowOperation, fastOperation);

            terminal.WriteLine();
            terminal.WriteLine("[M1] Both scheduled operations completed.");
        }
        catch (Exception exception)
        {
            terminal.WriteLine($"[M1] Demonstration failed: {exception.Message}");
        }
    }

    // Register game commands

    private static Task HelpAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: help");
            return Task.CompletedTask;
        }

        context.Terminal.WriteLine("Available commands:");
        foreach (var command in context.CommandRegistry.Commands.Where(c => !c.Debug))
            context.Terminal.WriteLine($"  {command.Usage,-22} {command.Description}");
        context.Terminal.WriteLine("  exit                   Exit NullByte");

        return Task.CompletedTask;
    }

    private static Task MoneyAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: money");
            return Task.CompletedTask;
        }

        context.Terminal.WriteLine($"Money: ${context.GameState.Player.Money:N2}");

        return Task.CompletedTask;
    }

    private static Task StatusAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: status");
            return Task.CompletedTask;
        }

        var server = context.NetworkService.GetCurrentServer();
        var player = context.GameState.Player;
        var hackingLevel = context.HackingFormulas.CalculateHackingLevel(player.HackingExperience);

        context.Terminal.WriteLine("Status");
        context.Terminal.WriteLine($"Money: ${player.Money:N2}");
        context.Terminal.WriteLine($"Hacking level: {hackingLevel}");
        context.Terminal.WriteLine($"Hacking XP: {player.HackingExperience:N2}");
        context.Terminal.WriteLine($"Connected to: {server.Hostname}");
        context.Terminal.WriteLine($"Root access: {(server.HasRootAccess ? "Yes" : "No")}");

        return Task.CompletedTask;
    }

    private static Task ListFilesAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: ls");
            return Task.CompletedTask;
        }

        context.Terminal.WriteLine("No files on this server.");

        return Task.CompletedTask;
    }

    private static Task ScanAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: scan");
            return Task.CompletedTask;
        }

        var currentServer = context.NetworkService.GetCurrentServer();
        var servers = context.NetworkService.Scan();

        context.Terminal.WriteLine($"Scanning from {currentServer.Hostname}...");

        if (servers.Count == 0)
        {
            context.Terminal.WriteLine("No connected servers found.");
            return Task.CompletedTask;
        }

        foreach (var server in servers)
            context.Terminal.WriteLine($"  {server.Hostname}");

        return Task.CompletedTask;
    }

    private static Task AnalyzeAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 1)
        {
            context.Terminal.WriteLine("Usage: analyze [hostname]");
            return Task.CompletedTask;
        }

        var server =
            arguments.Count == 1
                ? context.NetworkService.GetServer(arguments[0])
                : context.NetworkService.GetCurrentServer();

        if (context.NetworkService.IsServerKnown(server))
        {
            context.Terminal.WriteLine($"Server: {server.Hostname}");
            context.Terminal.WriteLine($"Root access: {(server.HasRootAccess ? "Yes" : "No")}");
            context.Terminal.WriteLine($"Required hacking level: {server.RequiredHackingLevel}");
            context.Terminal.WriteLine($"Money: ${server.Money:N2} / ${server.MaximumMoney:N2}");
            context.Terminal.WriteLine($"Security: {server.Security:F2} / {server.MinimumSecurity:F2}");
            context.Terminal.WriteLine($"Growth: {server.GrowthParameter:F2}");
            context.Terminal.WriteLine($"RAM: {server.UsedRam:F2} GB / {server.MaximumRam:F2} GB");
        }
        else
        {
            context.Terminal.WriteLine($"Unknown server {server.Hostname}");
        }

        return Task.CompletedTask;
    }

    private static Task ConnectAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            context.Terminal.WriteLine("Usage: connect <hostname>");
            return Task.CompletedTask;
        }

        var result = context.NetworkService.Connect(arguments[0]);
        if (!result.Succeeded)
        {
            context.Terminal.WriteLine(result.Error ?? "Connection failed.");
            return Task.CompletedTask;
        }

        context.Terminal.SetPrompt($"root@{result.Server!.Hostname} $ ");
        context.Terminal.WriteLine($"Connected to {result.Server!.Hostname}.");
        return Task.CompletedTask;
    }

    private static Task HomeAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: home");
            return Task.CompletedTask;
        }

        var result = context.NetworkService.GoHome();

        context.Terminal.SetPrompt($"root@{result.Server!.Hostname} $ ");
        context.Terminal.WriteLine($"Returned to {result.Server!.Hostname}.");

        return Task.CompletedTask;
    }

    private static Task NukeAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: nuke");
            return Task.CompletedTask;
        }

        var server = context.NetworkService.GetCurrentServer();
        if (server.HasRootAccess)
        {
            context.Terminal.WriteLine($"You already have root access to {server.Hostname}.");
            return Task.CompletedTask;
        }

        var player = context.GameState.Player;
        var hackingLevel = context.HackingFormulas.CalculateHackingLevel(player.HackingExperience);
        if (!server.TryGainRootAccess(hackingLevel))
        {
            context.Terminal.WriteLine($"Cannot gain root access to {server.Hostname}.");
            context.Terminal.WriteLine($"Required hacking level: {server.RequiredHackingLevel}");
            context.Terminal.WriteLine($"Your hacking level: {hackingLevel}");
        }
        else
        {
            context.Terminal.WriteLine($"Root access gained on {server.Hostname}.");
        }

        return Task.CompletedTask;
    }

    private static async Task HackAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: hack");
            return;
        }

        var server = context.NetworkService.GetCurrentServer();

        var player = context.GameState.Player;
        var duration = context.HackingFormulas.CalculateHackDuration(player, server);
        var clock = context.GameLoop.Clock;
        var startedAt = clock.Elapsed;
        context.Terminal.StartProgress($"Hacking {server.Hostname}...",
            () =>
            {
                var elapsed = clock.Elapsed - startedAt;
                return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
            });

        await RunWithInputBlockedAsync(
            context.Terminal,
            async () =>
            {
                var result = await context.ServerActions.HackAsync(server.Id, threads: 1, context.Terminal.StoppingToken);
                context.Terminal.StopProgress();

                if (result.Succeeded)
                    context.Terminal.WriteLine($"Hack succeeded. Stole ${result.MoneyStolen:N2}.");
                else
                    context.Terminal.WriteLine(result.FailureMessage);

                context.Terminal.WriteLine($"Experience gained: {result.ExperienceGained:N2}");
                context.Terminal.WriteLine($"Security increased by {result.SecurityIncrease:F3}.");
                context.Terminal.WriteLine($"Completed in {result.Duration.TotalSeconds:F1} game seconds.");
            });
    }

    private static async Task GrowAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: grow");
            return;
        }

        var server = context.NetworkService.GetCurrentServer();

        var player = context.GameState.Player;
        var duration = context.HackingFormulas.CalculateGrowDuration(player, server);
        var clock = context.GameLoop.Clock;
        var startedAt = clock.Elapsed;
        context.Terminal.StartProgress($"Growing {server.Hostname}...",
            () =>
            {
                var elapsed = clock.Elapsed - startedAt;
                return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
            });

        await RunWithInputBlockedAsync(
            context.Terminal,
            async () =>
            {
                var result = await context.ServerActions.GrowAsync(server.Id, threads: 1, context.Terminal.StoppingToken);
                context.Terminal.StopProgress();

                if (result.Succeeded)
                {
                    context.Terminal.WriteLine($"Grew server money by ${result.MoneyGrown:N2}.");
                    context.Terminal.WriteLine($"Experience gained: {result.ExperienceGained:N2}");
                    context.Terminal.WriteLine($"Security increased by {result.SecurityIncrease:F3}.");
                    context.Terminal.WriteLine($"Completed in {result.Duration.TotalSeconds:F1} game seconds.");
                }
                else
                {
                    context.Terminal.WriteLine(result.FailureMessage);
                }
            });
    }

    private static async Task WeakenAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: weaken");
            return;
        }

        var server = context.NetworkService.GetCurrentServer();

        var player = context.GameState.Player;
        var duration = context.HackingFormulas.CalculateWeakenDuration(player, server);
        var clock = context.GameLoop.Clock;
        var startedAt = clock.Elapsed;
        context.Terminal.StartProgress($"Weakening {server.Hostname}...",
            () =>
            {
                var elapsed = clock.Elapsed - startedAt;
                return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
            });

        await RunWithInputBlockedAsync(
            context.Terminal,
            async () =>
            {
                var result = await context.ServerActions.WeakenAsync(server.Id, threads: 1, context.Terminal.StoppingToken);
                context.Terminal.StopProgress();

                if (result.Succeeded)
                {
                    context.Terminal.WriteLine($"Security decreased by {result.SecurityDecrease:F3}.");
                    context.Terminal.WriteLine($"Experience gained: {result.ExperienceGained:N2}");
                    context.Terminal.WriteLine($"Completed in {result.Duration.TotalSeconds:F1} game seconds.");
                }
                else
                {
                    context.Terminal.WriteLine(result.FailureMessage);
                }
            });
    }

    private static Task ClearAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            context.Terminal.WriteLine("Usage: clear/cls");
            return Task.CompletedTask;
        }

        context.Terminal.Clear();

        return Task.CompletedTask;
    }

    // Support methods

    private static async Task DemonstrateOperationAsync(
        string name,
        TimeSpan duration,
        GameLoop gameLoop,
        IGameTerminal terminal)
    {
        var threadBefore = Environment.CurrentManagedThreadId;

        terminal.WriteLine(
            $"[M1] {name} started at " +
            $"{gameLoop.Clock.Elapsed.TotalSeconds:F3}s " +
            $"on thread {threadBefore}. " +
            $"Waiting {duration.TotalSeconds:F1} game seconds.");

        await gameLoop.Scheduler.DelayAsync(duration);

        var threadAfter = Environment.CurrentManagedThreadId;

        terminal.WriteLine(
            $"[M1] {name} completed at " +
            $"{gameLoop.Clock.Elapsed.TotalSeconds:F3}s " +
            $"on thread {threadAfter}.");

        if (threadBefore != threadAfter || !gameLoop.IsGameThread)
            throw new InvalidOperationException($"{name} resumed on the wrong thread.");
    }

    private static async Task RunWithInputBlockedAsync(IGameTerminal terminal, Func<Task> operation)
    {
        terminal.SetInputEnabled(false);

        try
        {
            await operation();
        }
        finally
        {
            terminal.SetInputEnabled(true);
        }
    }
}