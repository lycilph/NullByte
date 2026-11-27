using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class NetworkCommands(IGameClock clock, NetworkService networkService, NetworkFormulas networkFormulas, IGameTerminal terminal) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
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
                MinArgs = 0,
                MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.KnownServer }]
            });

        registry.Register(
            new CommandDefinition(
                "connect",
                "connect <hostname>",
                "Connect to a discovered server.",
                ConnectAsync)
            {
                MinArgs = 1,
                MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.ConnectableServer }]
            });

        registry.Register(
            new CommandDefinition(
                "home",
                "home",
                "Return to your home server.",
                HomeAsync));
    }

    private async Task ScanAsync(IReadOnlyList<string> arguments)
    {
        var currentServer = networkService.GetCurrentServer();
        var servers = networkService.Scan(currentServer.Hostname);

        terminal.WriteLine($"Scanning from {currentServer.Hostname}...");

        if (servers.Count == 0)
        {
            terminal.WriteLine("No connected servers found.");
            return;
        }

        foreach (var server in servers)
            terminal.WriteLine($"  {server.Hostname}");
    }

    private async Task AnalyzeAsync(IReadOnlyList<string> arguments)
    {
        var server =
            arguments.Count == 1
                ? networkService.GetServer(arguments[0])
                : networkService.GetCurrentServer();

        var duration = networkFormulas.CalculateAnalyzeDuration();
        var startedAt = clock.Elapsed;
        terminal.StartProgress($"Analyzing {server.Hostname}",
            () =>
            {
                var elapsed = clock.Elapsed - startedAt;
                return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
            });

        NetworkAnalyzeResult result;
        try { result = await networkService.AnalyzeAsync(server.Hostname); }
        finally { terminal.StopProgress(); }

        if (result.Succeeded)
        {
            terminal.WriteLine($"Server: {result.Hostname}");
            terminal.WriteLine($"Root access: {(result.HasRootAccess ? "Yes" : "No")}");
            terminal.WriteLine($"Backdoor: {(result.HasBackdoor ? "Yes" : "No")}");
            terminal.WriteLine($"Required hacking level: {result.RequiredHackingLevel}");
            terminal.WriteLine($"Money: ${result.Money:N2} / ${result.MaximumMoney:N2}");
            terminal.WriteLine($"Security: {result.Security:F2} / {result.MinimumSecurity:F2}");
            terminal.WriteLine($"Growth: {result.Growth:F2}");
            terminal.WriteLine($"RAM: {result.UsedRam:F2} GB / {result.MaximumRam:F2} GB");
        }
        else
            terminal.WriteLine(result.FailureMessage);
    }

    private async Task ConnectAsync(IReadOnlyList<string> arguments)
    {
        var result = networkService.Connect(arguments[0]);
        if (!result.Succeeded)
        {
            terminal.WriteLine(result.Error ?? "Connection failed.");
            return;
        }

        terminal.WriteLine($"Connected to {result.Server!.Hostname}.");
    }

    private async Task HomeAsync(IReadOnlyList<string> arguments)
    {
        var result = networkService.GoHome();

        terminal.WriteLine($"Returned to {result.Server!.Hostname}.");
    }
}
