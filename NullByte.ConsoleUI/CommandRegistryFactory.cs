using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Simulation;
using NullByte.Core.Time;
using NullByte.Scripting.Compilation;

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
                "cat",
                "cat <filename>",
                "Display a file's contents.",
                CatAsync));

        registry.Register(
            new CommandDefinition(
                "rm",
                "rm <filename>",
                "Delete a file.",
                RemoveFileAsync));

        registry.Register(
            new CommandDefinition(
                "edit",
                "edit <filename>",
                "Create or replace a file.",
                EditFileAsync));

        registry.Register(
            new CommandDefinition(
                "run",
                "run <script> [args...]",
                "Run a script.",
                RunScriptAsync)
            {
                Completions =
                [
                    new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename}
                ]
            });

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
            {
                Completions =
                [ 
                    new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.KnownServer }
                ]
            });

        registry.Register(
            new CommandDefinition(
                "connect",
                "connect <hostname>",
                "Connect to a discovered server.",
                ConnectAsync)
            {
                Completions =
                [
                    new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.ConnectableServer }
                ]
            });

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
                "botnet",
                "botnet <subcommand>",
                "Manage your botnet (status, buy, upgrade).",
                BotnetAsync)
            {
                Completions =
                [
                    new ArgumentCompletion { ArgumentIndex = 0, Values = ["status","buy", "upgrade"]},
                    new ArgumentCompletion { ArgumentIndex = 1, Kind = CompletionKind.BotnetNode,
                        When = new CompletionCondition(ArgumentIndex: 0,Value: "upgrade")},
                    new ArgumentCompletion { ArgumentIndex = 2, Values = ["level", "ram", "core"],
                        When = new CompletionCondition(ArgumentIndex: 0, Value: "upgrade")}
                ]
            });

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

        var clock = context.Clock;
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
                    context.Clock,
                    terminal);

            var fastOperation =
                DemonstrateOperationAsync(
                    name: "operation-b",
                    duration: TimeSpan.FromSeconds(1.5),
                    context.GameLoop,
                    context.Clock,
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

        var server = context.NetworkService.GetCurrentServer();
        var files = context.GameState.FileSystem.ListFiles(server.Hostname);

        if (files.Count == 0)
        {
            context.Terminal.WriteLine("No files on this server.");
            return Task.CompletedTask;
        }

        foreach (var file in files)
            context.Terminal.WriteLine(file);

        return Task.CompletedTask;
    }

    private static Task CatAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            context.Terminal.WriteLine("Usage: cat <filename>");
            return Task.CompletedTask;
        }

        var server = context.NetworkService.GetCurrentServer();

        if (!context.GameState.FileSystem.TryReadFile(server.Hostname, arguments[0], out var content))
        {
            context.Terminal.WriteLine($"File not found: {arguments[0]}");

            return Task.CompletedTask;
        }

        context.Terminal.WriteLine(content!);

        return Task.CompletedTask;
    }

    private static Task RemoveFileAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            context.Terminal.WriteLine("Usage: rm <filename>");
            return Task.CompletedTask;
        }

        var server = context.NetworkService.GetCurrentServer();
        var fileName = arguments[0];

        if (!context.GameState.FileSystem.DeleteFile(server.Hostname, fileName))
        {
            context.Terminal.WriteLine($"File not found: {fileName}");

            return Task.CompletedTask;
        }

        context.Terminal.WriteLine($"Deleted {fileName}.");

        return Task.CompletedTask;
    }

    private static Task EditFileAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            context.Terminal.WriteLine("Usage: edit <filename>");
            return Task.CompletedTask;
        }

        var server = context.NetworkService.GetCurrentServer();
        var fileName = "test.cx"; // arguments[0];
        var source =
@"nb.Print($""Hacking n00dles"");
var result = await nb.Hack(""n00dles"");
if (!result.Succeeded)
    nb.Print($""{result.FailureMessage}"");
nb.Print($""Operation done"");"; 
//@"var target = args[0];
//nb.Print($""Starting against {target}"");
//int iteration = 0;

//while (true)
//{
//    nb.Print($""Iteration {iteration}"");
//    iteration++;

//    if (nb.GetServerSecurityLevel(target) > nb.GetServerMinSecurityLevel(target) + 5)
//    {
//        nb.Print($""Weakening {target}"");
//        await nb.Weaken(target);
//    }
//    else if (nb.GetServerMoneyAvailable(target) < nb.GetServerMaxMoney(target) * 0.75)
//    {
//        nb.Print($""Growing {target}"");
//        await nb.Grow(target);
//    }
//    else
//    {
//        nb.Print($""Hacking {target}"");
//        await nb.Hack(target);
//    }

//    nb.Print($""Operation done"");
//}";

        context.GameState.FileSystem.WriteFile(server.Hostname, fileName, source);

        context.Terminal.WriteLine($"Saved {fileName}.");

        return Task.CompletedTask;
    }

    private static Task RunScriptAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            context.Terminal.WriteLine("Usage: run <script> [args...]");
            return Task.CompletedTask;
        }

        var server = context.NetworkService.GetCurrentServer();
        var fileName = arguments[0];
        var scriptArguments = arguments.Skip(1).ToArray();

        var result = context.ScriptService.Start(
            server.Hostname,
            fileName,
            scriptArguments);

        foreach (var diagnostic in result.Diagnostics)
        {
            WriteScriptDiagnostic(
                context.Terminal,
                fileName,
                diagnostic);
        }

        if (!result.Succeeded)
        {
            if (result.Diagnostics.Count == 0)
                context.Terminal.WriteLine(result.FailureMessage);

            return Task.CompletedTask;
        }

        context.Terminal.WriteLine($"Started {fileName}.");

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
        var clock = context.Clock;
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
                var result = await context.ServerActions.HackAsync(server.Hostname, threads: 1, context.Terminal.StoppingToken);
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
        var clock = context.Clock;
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
                var result = await context.ServerActions.GrowAsync(server.Hostname, threads: 1, context.Terminal.StoppingToken);
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
        var clock = context.Clock;
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
                var result = await context.ServerActions.WeakenAsync(server.Hostname, threads: 1, context.Terminal.StoppingToken);
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

    private static Task BotnetAsync(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        var subcommand = arguments.Count == 0
            ? "status"
            : arguments[0].ToLowerInvariant();

        switch (subcommand)
        {
            case "status" when arguments.Count <= 1:
                WriteBotnetStatus(context);
                break;

            case "buy" when arguments.Count == 1:
                BuyBotnetNode(context);
                break;

            case "upgrade":
                UpgradeBotnetNode(context, arguments);
                break;

            default:
                WriteBotnetUsage(context);
                break;
        }

        return Task.CompletedTask;
    }

    private static void WriteBotnetUsage(CommandContext context)
    {
        context.Terminal.WriteLine("Usage:");
        context.Terminal.WriteLine("  botnet [status]");
        context.Terminal.WriteLine("  botnet buy");
        context.Terminal.WriteLine("  botnet upgrade <node> level|ram|core");
    }

    private static void WriteBotnetStatus(CommandContext context)
    {
        var terminal = context.Terminal;
        var botnet = context.GameState.Botnet;
        var formulas = context.BotnetFormulas;
        var service = context.BotnetService;

        terminal.WriteLine("Botnet");
        terminal.WriteLine($"Nodes: {botnet.NodeCount}");
        terminal.WriteLine($"Production: ${formulas.CalculateTotalProduction(botnet):N2}/s");
        terminal.WriteLine($"Total earned: ${botnet.TotalEarned:N2}");

        if (botnet.NodeCount > 0)
        {
            terminal.WriteLine();
            terminal.WriteLine(
                $"  {"#",3} {"Level",5} {"RAM",4} {"Cores",5} {"$/s",10}  " +
                $"{"+Level",12} {"+RAM",12} {"+Core",12}");

            for (var index = 0; index < botnet.NodeCount; index++)
            {
                var node = botnet.Nodes[index];

                terminal.WriteLine(
                    $"  {index + 1,3} {node.Level,5} {node.Ram,4} {node.Cores,5} " +
                    $"{formulas.CalculateNodeProduction(node),10:N2}  " +
                    $"{FormatUpgradeCost(service.GetUpgradeCost(index, BotnetUpgradeKind.Level)),12} " +
                    $"{FormatUpgradeCost(service.GetUpgradeCost(index, BotnetUpgradeKind.Ram)),12} " +
                    $"{FormatUpgradeCost(service.GetUpgradeCost(index, BotnetUpgradeKind.Cores)),12}");
            }
        }

        terminal.WriteLine();
        terminal.WriteLine($"Next node costs ${service.GetNextNodeCost():N2}.");
    }

    private static void BuyBotnetNode(CommandContext context)
    {
        var terminal = context.Terminal;
        var result = context.BotnetService.PurchaseNode();

        if (!result.Succeeded)
        {
            terminal.WriteLine(result.FailureMessage);
            return;
        }

        terminal.WriteLine($"Bought botnet node #{result.NodeIndex + 1} for ${result.Cost:N2}.");
        WriteBotnetProduction(context);
    }

    private static void UpgradeBotnetNode(
        CommandContext context,
        IReadOnlyList<string> arguments)
    {
        var terminal = context.Terminal;

        if (arguments.Count != 3 ||
            !int.TryParse(arguments[1], out var nodeNumber) ||
            !TryParseBotnetUpgradeKind(arguments[2], out var kind))
        {
            WriteBotnetUsage(context);
            return;
        }

        // Players count nodes from one; the model counts from zero.
        var result = context.BotnetService.Upgrade(nodeNumber - 1, kind);

        if (!result.Succeeded)
        {
            terminal.WriteLine(result.FailureMessage);
            return;
        }

        var node = context.GameState.Botnet.Nodes[result.NodeIndex];
        var description = kind switch
        {
            BotnetUpgradeKind.Level => $"level to {node.Level}",
            BotnetUpgradeKind.Ram => $"RAM to {node.Ram}",
            _ => $"cores to {node.Cores}"
        };

        terminal.WriteLine($"Upgraded node #{result.NodeIndex + 1} {description} for ${result.Cost:N2}.");
        WriteBotnetProduction(context);
    }

    private static void WriteBotnetProduction(CommandContext context)
    {
        var production = context.BotnetFormulas.CalculateTotalProduction(context.GameState.Botnet);

        context.Terminal.WriteLine($"Botnet production is now ${production:N2}/s.");
    }

    private static bool TryParseBotnetUpgradeKind(string text, out BotnetUpgradeKind kind)
    {
        switch (text.ToLowerInvariant())
        {
            case "level":
                kind = BotnetUpgradeKind.Level;
                return true;
            case "ram":
                kind = BotnetUpgradeKind.Ram;
                return true;
            case "core":
            case "cores":
                kind = BotnetUpgradeKind.Cores;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static string FormatUpgradeCost(double? cost)
        => cost is null ? "max" : $"${cost.Value:N0}";

    // Support methods

    private static async Task DemonstrateOperationAsync(
        string name,
        TimeSpan duration,
        GameLoop gameLoop,
        IGameClock clock,
        IGameTerminal terminal)
    {
        var threadBefore = Environment.CurrentManagedThreadId;

        terminal.WriteLine(
            $"[M1] {name} started at " +
            $"{clock.Elapsed.TotalSeconds:F3}s " +
            $"on thread {threadBefore}. " +
            $"Waiting {duration.TotalSeconds:F1} game seconds.");

        await gameLoop.Scheduler.DelayAsync(duration);

        var threadAfter = Environment.CurrentManagedThreadId;

        terminal.WriteLine(
            $"[M1] {name} completed at " +
            $"{clock.Elapsed.TotalSeconds:F3}s " +
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

    private static void WriteScriptDiagnostic(IGameTerminal terminal, string fileName, ScriptDiagnostic diagnostic)
    {
        var severity = diagnostic.Severity.ToString().ToLowerInvariant();

        terminal.WriteLine(
            $"{fileName}({diagnostic.Line},{diagnostic.Column}): " +
            $"{severity} {diagnostic.Id}: {diagnostic.Message}");
    }
}