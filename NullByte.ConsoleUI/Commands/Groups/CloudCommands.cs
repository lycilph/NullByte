using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class CloudCommands(CloudServerService service, IGameOutput output) : ICommandGroup
{
    private const string BuyUsage = "cloud buy <hostname> [ram]";
    private const string UpgradeUsage = "cloud upgrade <hostname> <ram>";
    private const string RenameUsage = "cloud rename <hostname> <newHostname>";

    public void Register(CommandRegistry registry)
    {
        registry.Register(new CommandDefinition(
            "cloud",
            "cloud [list]",
            "Show your cloud servers.",
            ListAsync)
        { Aliases = ["cloud list"] });

        registry.Register(new CommandDefinition(
            "cloud buy",
            BuyUsage,
            "Purchase a cloud server.",
            BuyAsync)
        { MinArgs = 1, MaxArgs = 2 });

        registry.Register(new CommandDefinition(
            "cloud upgrade",
            UpgradeUsage,
            "Upgrade a cloud server's RAM.",
            UpgradeAsync)
        {
            MinArgs = 2,
            MaxArgs = 2,
            Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.CloudServer }]
        });

        registry.Register(new CommandDefinition(
            "cloud rename",
            RenameUsage,
            "Rename a cloud server.",
            RenameAsync)
        {
            MinArgs = 2,
            MaxArgs = 2,
            Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.CloudServer }]
        });
    }

    private Task ListAsync(IReadOnlyList<string> arguments)
    {
        var servers = service.GetServers();
        output.WriteLine("Cloud servers");
        output.WriteLine($"Servers: {servers.Count}/{service.MaximumCount}");

        if (servers.Count == 0)
            output.WriteLine("  No cloud servers. Use 'cloud buy <hostname> [ram]' to purchase one.");
        else
        {
            output.WriteLine($"  {"Hostname",-20} {"RAM",8} {"Used",8} {"Next upgrade",16}");
            foreach (var server in servers)
            {
                var nextRam = (int)server.MaximumRam * 2;
                var nextCost = service.GetUpgradeCost(server.Hostname, nextRam);
                output.WriteLine(
                    $"  {server.Hostname,-20} {server.MaximumRam,5:N0} GB {server.UsedRam,5:N2} GB " +
                    $"{(nextCost is null ? "max" : $"\u0024{nextCost.Value:N0}"),16}");
            }
        }

        output.WriteLine($"Next server costs \u0024{service.GetPurchaseCost():N0} (8 GB).");
        return Task.CompletedTask;
    }

    private Task BuyAsync(IReadOnlyList<string> arguments)
    {
        var ram = arguments.Count == 2 ? ParseRam(arguments[1]) : 8;
        if (ram is null)
        {
            WriteInvalidRam(arguments[1]);
            return Task.CompletedTask;
        }

        var result = service.Purchase(arguments[0], ram.Value);
        if (!result.Succeeded)
        {
            output.WriteLine(result.FailureMessage);
            return Task.CompletedTask;
        }

        output.WriteLine($"Bought cloud server '{result.Server!.Hostname}' with {result.Server.MaximumRam:N0} GB RAM for \u0024{result.Cost:N0}.");
        return Task.CompletedTask;
    }

    private Task UpgradeAsync(IReadOnlyList<string> arguments)
    {
        var ram = ParseRam(arguments[1]);
        if (ram is null)
        {
            WriteInvalidRam(arguments[1]);
            return Task.CompletedTask;
        }

        var result = service.Upgrade(arguments[0], ram.Value);
        if (!result.Succeeded)
        {
            output.WriteLine(result.FailureMessage);
            return Task.CompletedTask;
        }

        output.WriteLine($"Upgraded '{result.Server!.Hostname}' to {result.Server.MaximumRam:N0} GB RAM for \u0024{result.Cost:N0}.");
        return Task.CompletedTask;
    }

    private Task RenameAsync(IReadOnlyList<string> arguments)
    {
        var result = service.Rename(arguments[0], arguments[1]);
        if (!result.Succeeded)
        {
            output.WriteLine(result.FailureMessage);
            return Task.CompletedTask;
        }

        output.WriteLine($"Renamed cloud server to '{result.Server!.Hostname}'.");
        return Task.CompletedTask;
    }

    private void WriteInvalidRam(string value)
    {
        var supportedTiers = string.Join(", ", CloudServerFormulas.RamTiers);
        output.WriteLine($"Invalid RAM tier '{value}'. Supported tiers: {supportedTiers} GB.");
    }

    private static int? ParseRam(string value)
        => int.TryParse(value, out var ram) && CloudServerFormulas.IsSupportedRam(ram) ? ram : null;
}
