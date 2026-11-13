using NullByte.Core.Domain;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class DebugCommands(NetworkService networkService, VirtualFileSystem fileSystem, IGameClock clock, CommandRegistry registry, IGameOutput output) : ICommandGroup
{
    public void Register(CommandRegistry registry)
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
                "scripts",
                "scripts",
                "Create test scripts.",
                TestScriptsAsync)
            { Debug = true });
    }

    private async Task DebugAsync(IReadOnlyList<string> arguments)
    {
        output.WriteLine("Debugging commands:");
        foreach (var command in registry.Commands.Where(c => c.Debug))
            output.WriteLine($"  {command.Usage,-22} {command.Description}");
    }

    private async Task TimeAsync(IReadOnlyList<string> arguments)
    {
        output.WriteLine($"Game time: {clock.Elapsed.TotalSeconds:F3}s");
    }

    private async Task TestScriptsAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var fileName = "single_hack_test.cx";
        var source =
@"nb.Print($""Hacking n00dles"");
var result = await nb.Hack(""n00dles"");
if (!result.Succeeded)
    nb.Print($""{result.FailureMessage}"");
nb.Print($""Operation done"");";

        fileSystem.WriteFile(server.Hostname, fileName, source);
        output.WriteLine($"{fileName} created.");

        fileName = "basic_hack.cx";
        source =
@"var target = args[0];
nb.Print($""Starting against {target}"");
int iteration = 0;

while (true)
{
    nb.Print($""Iteration {iteration}"");
    iteration++;

    if (nb.GetServerSecurityLevel(target) > nb.GetServerMinSecurityLevel(target) + 5)
    {
        nb.Print($""Weakening {target}"");
        var result = await nb.Weaken(target);
        if (!result.Succeeded)
        {
            nb.Print($""{result.FailureMessage}"");
            return;
        }
    }
    else if (nb.GetServerMoneyAvailable(target) < nb.GetServerMaxMoney(target) * 0.75)
    {
        nb.Print($""Growing {target}"");
        var result = await nb.Grow(target);
        if (!result.Succeeded)
        {
            nb.Print($""{result.FailureMessage}"");
            return;
        }
    }
    else
    {
        nb.Print($""Hacking {target}"");
        var result = await nb.Hack(target);
        if (!result.Succeeded)
        {
            nb.Print($""{result.FailureMessage}"");
            return;
        }
    }

    nb.Print($""Operation done"");
}";

        fileSystem.WriteFile(server.Hostname, fileName, source);
        output.WriteLine($"{fileName} created.");
    }
}
