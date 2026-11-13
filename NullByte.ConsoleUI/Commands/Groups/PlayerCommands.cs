using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class PlayerCommands(Player player, NetworkService networkService, HackingFormulas hackingFormulas, IGameOutput output) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
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
    }

    private async Task MoneyAsync(IReadOnlyList<string> arguments)
    {
        output.WriteLine($"Money: ${player.Money:N2}");
    }

    private async Task StatusAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var hackingLevel = hackingFormulas.CalculateHackingLevel(player.HackingExperience);

        output.WriteLine("Status");
        output.WriteLine($"Money: ${player.Money:N2}");
        output.WriteLine($"Hacking level: {hackingLevel}");
        output.WriteLine($"Hacking XP: {player.HackingExperience:N2}");
        output.WriteLine($"Connected to: {server.Hostname}");
        output.WriteLine($"Root access: {(server.HasRootAccess ? "Yes" : "No")}");
    }
}
