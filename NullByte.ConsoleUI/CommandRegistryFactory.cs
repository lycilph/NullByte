using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Services;
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
        
    }


    private static void RegisterGameCommands(CommandRegistry registry)
    {
      

        

        

       

        

        //registry.Register(
        //    new CommandDefinition(
        //        "botnet",
        //        "botnet <subcommand>",
        //        "Manage your botnet (status, buy, upgrade).",
        //        BotnetAsync)
        //    {
        //        ArgumentsCountCheck = c => c >= 0,
        //        Completions =
        //        [
        //            new ArgumentCompletion { ArgumentIndex = 0, Values = ["status","buy", "upgrade"]},
        //            new ArgumentCompletion { ArgumentIndex = 1, Kind = CompletionKind.BotnetNode,
        //                When = new CompletionCondition(ArgumentIndex: 0,Value: "upgrade")},
        //            new ArgumentCompletion { ArgumentIndex = 2, Values = ["level", "ram", "core"],
        //                When = new CompletionCondition(ArgumentIndex: 0, Value: "upgrade")}
        //        ]
        //    });

        
    }

    // Register debug commands

    

    // Register game commands


   

   

  

    

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

    
}