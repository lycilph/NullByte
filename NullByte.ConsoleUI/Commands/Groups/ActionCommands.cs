using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class ActionCommands(Player player, IGameClock clock, HackingFormulas hackingFormulas, ServerActions serverActions, NetworkService networkService, IGameTerminal terminal) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
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
    }

    private async Task NukeAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        if (server.HasRootAccess)
        {
            terminal.WriteLine($"You already have root access to {server.Hostname}.");
            return;
        }

        var hackingLevel = hackingFormulas.CalculateHackingLevel(player.HackingExperience);
        if (!server.TryGainRootAccess(hackingLevel))
        {
            terminal.WriteLine($"Cannot gain root access to {server.Hostname}.");
            terminal.WriteLine($"Required hacking level: {server.RequiredHackingLevel}");
            terminal.WriteLine($"Your hacking level: {hackingLevel}");
        }
        else
        {
            terminal.WriteLine($"Root access gained on {server.Hostname}.");
        }

        return;
    }

    private async Task HackAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();

        var duration = hackingFormulas.CalculateHackDuration(player, server);
        var startedAt = clock.Elapsed;
        terminal.StartProgress($"Hacking {server.Hostname}",
            () =>
            {
                var elapsed = clock.Elapsed - startedAt;
                return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
            });

        HackResult result;
        try { result = await serverActions.HackAsync(server.Hostname, threads: 1, terminal.StoppingToken); }
        finally { terminal.StopProgress(); }

        if (result.Succeeded)
            terminal.WriteLine($"Hack succeeded. Stole ${result.MoneyStolen:N2}.");
        else
            terminal.WriteLine(result.FailureMessage);

        terminal.WriteLine($"Experience gained: {result.ExperienceGained:N2}");
        terminal.WriteLine($"Security increased by {result.SecurityIncrease:F3}.");
        terminal.WriteLine($"Completed in {result.Duration.TotalSeconds:F1} game seconds.");
    }

    private async Task GrowAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();

        var duration = hackingFormulas.CalculateGrowDuration(player, server);
        var startedAt = clock.Elapsed;
        terminal.StartProgress($"Growing {server.Hostname}",
            () =>
            {
                var elapsed = clock.Elapsed - startedAt;
                return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
            });

        GrowResult result;
        try { result = await serverActions.GrowAsync(server.Hostname, threads: 1, terminal.StoppingToken); }
        finally { terminal.StopProgress(); }

        if (result.Succeeded)
        {
            terminal.WriteLine($"Grew server money by ${result.MoneyGrown:N2}.");
            terminal.WriteLine($"Experience gained: {result.ExperienceGained:N2}");
            terminal.WriteLine($"Security increased by {result.SecurityIncrease:F3}.");
            terminal.WriteLine($"Completed in {result.Duration.TotalSeconds:F1} game seconds.");
        }
        else
        {
            terminal.WriteLine(result.FailureMessage);
        }
    }

    private async Task WeakenAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();

        var duration = hackingFormulas.CalculateWeakenDuration(player, server);
        var startedAt = clock.Elapsed;
        terminal.StartProgress($"Weakening {server.Hostname}",
            () =>
            {
                var elapsed = clock.Elapsed - startedAt;
                return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
            });

        WeakenResult result;
        try { result = await serverActions.WeakenAsync(server.Hostname, threads: 1, terminal.StoppingToken); }
        finally { terminal.StopProgress(); }

        if (result.Succeeded)
        {
            terminal.WriteLine($"Security decreased by {result.SecurityDecrease:F3}.");
            terminal.WriteLine($"Experience gained: {result.ExperienceGained:N2}");
            terminal.WriteLine($"Completed in {result.Duration.TotalSeconds:F1} game seconds.");
        }
        else
        {
            terminal.WriteLine(result.FailureMessage);
        }
    }
}
