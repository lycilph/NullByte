using NullByte.Core.Domain;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Editor;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class DebugCommands(
    Player player,
    IGameClock clock,
    CommandRegistry registry,
    IGameOutput output,
    IExternalEditorStatus? externalEditorStatus) : ICommandGroup
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
                "cheat",
                "cheat",
                "Add money and experience to player.",
                CheatAsync)
            { Debug = true });

        registry.Register(
            new CommandDefinition(
                "editor",
                "editor",
                "Shows external editor connection status.",
                EditorAsync)
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

    private async Task CheatAsync(IReadOnlyList<string> arguments)
    {
        player.AddMoney(1000);
        player.AddHackingExperience(1000);

        output.WriteLine($"Player - money: {player.Money}, experience: {player.HackingExperience}");
    }

    private async Task EditorAsync(IReadOnlyList<string> arguments)
    {
        if (externalEditorStatus is null)
        {
            output.WriteLine("External editor integration is not running.");
            return;
        }
        var status = externalEditorStatus.GetStatus();

        if (status.Connections.Count == 0)
        {
            output.WriteLine("External editor integration is running, but no external editors are connected.");
            return;
        }

        output.WriteLine("External editor integration is running.");
        output.WriteLine($"Connected editors: {status.Connections.Count}");
        output.WriteLine();

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < status.Connections.Count; i++)
        {
            var connection = status.Connections[i];
            var duration = now - connection.ConnectedAt;

            output.WriteLine($"  #{i + 1}  connected {FormatDuration(duration)} ago");
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}d {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";

        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }
}