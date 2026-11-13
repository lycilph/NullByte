using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class BotnetCommands(Botnet botnet, BotnetService service, BotnetFormulas formulas, IGameOutput output) : ICommandGroup
{
    private const string UpgradeUsage = "botnet upgrade <node> level|ram|core";
    private static readonly Dictionary<string, BotnetUpgradeKind> UpgradeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["level"] = BotnetUpgradeKind.Level,
        ["ram"] = BotnetUpgradeKind.Ram,
        ["core"] = BotnetUpgradeKind.Cores,
        ["cores"] = BotnetUpgradeKind.Cores,
    };

    public void Register(CommandRegistry registry)
    {
        // Bare "botnet" and "botnet status" are the same command.
        registry.Register(new CommandDefinition(
            "botnet",
            "botnet [status]",
            "Show your botnet.",
            StatusAsync)
        { Aliases = ["botnet status"] });

        registry.Register(new CommandDefinition(
            "botnet buy",
            "botnet buy",
            "Buy a new botnet node.",
            BuyAsync));

        registry.Register(new CommandDefinition(
            "botnet upgrade",
            UpgradeUsage,
            "Upgrade a botnet node.",
            UpgradeAsync)
        {
            MinArgs = 2, MaxArgs = 2,
            Completions =
                [
                    new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.BotnetNode },
                    new ArgumentCompletion { ArgumentIndex = 1, Values = ["level", "ram", "core"] }
                ]
        });
    }

    private async Task StatusAsync(IReadOnlyList<string> arguments)
    {
        output.WriteLine("Botnet");
        output.WriteLine($"Nodes: {botnet.NodeCount}");
        output.WriteLine($"Production: ${formulas.CalculateTotalProduction(botnet):N2}/s");
        output.WriteLine($"Total earned: ${botnet.TotalEarned:N2}");

        if (botnet.NodeCount > 0)
        {
            output.WriteLine();
            output.WriteLine(
                $"  {"#",3} {"Level",5} {"RAM",4} {"Cores",5} {"$/s",10}  " +
                $"{"+Level",12} {"+RAM",12} {"+Core",12}");

            for (var i = 0; i < botnet.NodeCount; i++)
            {
                var node = botnet.Nodes[i];
                output.WriteLine(
                    $"  {i + 1,3} {node.Level,5} {node.Ram,4} {node.Cores,5} " +
                    $"{formulas.CalculateNodeProduction(node),10:N2}  " +
                    $"{FormatCost(service.GetUpgradeCost(i, BotnetUpgradeKind.Level)),12} " +
                    $"{FormatCost(service.GetUpgradeCost(i, BotnetUpgradeKind.Ram)),12} " +
                    $"{FormatCost(service.GetUpgradeCost(i, BotnetUpgradeKind.Cores)),12}");
            }
        }

        output.WriteLine();
        output.WriteLine($"Next node costs ${service.GetNextNodeCost():N2}.");
    }

    private async Task BuyAsync(IReadOnlyList<string> arguments)
    {
        var result = service.PurchaseNode();
        if (!result.Succeeded)
        {
            output.WriteLine(result.FailureMessage);
            return;
        }

        output.WriteLine($"Bought botnet node #{result.NodeIndex + 1} for ${result.Cost:N2}.");
        WriteProduction();
    }

    private async Task UpgradeAsync(IReadOnlyList<string> args)
    {
        if (!int.TryParse(args[0], out var nodeNumber) || !UpgradeKinds.TryGetValue(args[1], out var kind))
        {
            output.WriteLine($"Usage: {UpgradeUsage}");
            return;
        }

        // Players count nodes from one; the model counts from zero.
        var result = service.Upgrade(nodeNumber - 1, kind);
        if (!result.Succeeded)
        {
            output.WriteLine(result.FailureMessage);
            return;
        }

        var node = botnet.Nodes[result.NodeIndex];
        var what = kind switch
        {
            BotnetUpgradeKind.Level => $"level to {node.Level}",
            BotnetUpgradeKind.Ram => $"RAM to {node.Ram}",
            _ => $"cores to {node.Cores}"
        };

        output.WriteLine($"Upgraded node #{result.NodeIndex + 1} {what} for ${result.Cost:N2}.");
        WriteProduction();
    }

    private void WriteProduction()
        => output.WriteLine($"Botnet production is now ${formulas.CalculateTotalProduction(botnet):N2}/s.");

    private static string FormatCost(double? cost) => cost is null ? "max" : $"${cost.Value:N0}";
}
