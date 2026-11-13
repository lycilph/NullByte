using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class NetworkCommands(NetworkService networkService, IGameOutput output) : ICommandGroup
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
                MinArgs = 0, MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.KnownServer }]
            });

        registry.Register(
            new CommandDefinition(
                "connect",
                "connect <hostname>",
                "Connect to a discovered server.",
                ConnectAsync)
            {
                MinArgs = 1, MaxArgs = 1,
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
        var servers = networkService.Scan();

        output.WriteLine($"Scanning from {currentServer.Hostname}...");

        if (servers.Count == 0)
        {
            output.WriteLine("No connected servers found.");
            return;
        }

        foreach (var server in servers)
            output.WriteLine($"  {server.Hostname}");
    }

    private async Task AnalyzeAsync(IReadOnlyList<string> arguments)
    {
        var server =
            arguments.Count == 1
                ? networkService.GetServer(arguments[0])
                : networkService.GetCurrentServer();

        if (networkService.IsServerKnown(server))
        {
            output.WriteLine($"Server: {server.Hostname}");
            output.WriteLine($"Root access: {(server.HasRootAccess ? "Yes" : "No")}");
            output.WriteLine($"Required hacking level: {server.RequiredHackingLevel}");
            output.WriteLine($"Money: ${server.Money:N2} / ${server.MaximumMoney:N2}");
            output.WriteLine($"Security: {server.Security:F2} / {server.MinimumSecurity:F2}");
            output.WriteLine($"Growth: {server.GrowthParameter:F2}");
            output.WriteLine($"RAM: {server.UsedRam:F2} GB / {server.MaximumRam:F2} GB");
        }
        else
        {
            output.WriteLine($"Unknown server {server.Hostname}");
        }
    }

    private async Task ConnectAsync(IReadOnlyList<string> arguments)
    {
        var result = networkService.Connect(arguments[0]);
        if (!result.Succeeded)
        {
            output.WriteLine(result.Error ?? "Connection failed.");
            return;
        }

        output.WriteLine($"Connected to {result.Server!.Hostname}.");
    }

    private async Task HomeAsync(IReadOnlyList<string> arguments)
    {
        var result = networkService.GoHome();

        output.WriteLine($"Returned to {result.Server!.Hostname}.");
    }
}
