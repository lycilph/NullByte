using System.Diagnostics;
using NullByte.ConsoleUI.Configuration;
using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class SystemCommands(
    CommandRegistry registry,
    IGameTerminal terminal,
    HostRequestMailbox hostRequests,
    AppSettings? settings,
    NullByte.Core.Narrative.NarrativeCoordinator narrative) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
        registry.Register(
            new CommandDefinition(
                "journal",
                "journal",
                "Review tutorial guidance for capabilities already unlocked.",
                JournalAsync));

        registry.Register(
            new CommandDefinition(
                "help",
                "help",
                "Show this help.",
                HelpAsync));

        registry.Register(
            new CommandDefinition(
               "exit",
               "exit",
               "Exit NullByte.",
               RequestShutdownAsync)
            { Aliases = ["quit"] });

        registry.Register(
            new CommandDefinition(
                "clear",
                "clear",
                "Clear the terminal.",
                ClearAsync)
            { Aliases = ["cls"] });

        if (settings != null && settings.Editor.Enabled)
        {
            registry.Register(
                new CommandDefinition(
                    "vscode",
                    "vscode",
                    "Opens the VS Code editor.",
                    VSCodeAsync));
        }
    }

    private async Task VSCodeAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine("Launching VS Code...");
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c code -n --folder-uri \"nullbyte:/NullByte/\"",
            UseShellExecute = false,
            CreateNoWindow = true // Prevents a black console window from flashing
        };

        Process.Start(startInfo);
    }

    private async Task JournalAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine("SYSTEM JOURNAL");
        terminal.WriteLine();

        foreach (var message in narrative.GetTutorialGuide())
        {
            terminal.WriteLine(message.Text);
            terminal.WriteLine();
        }
    }

    private async Task HelpAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine("Available commands:");
        foreach (var command in registry.Commands.Where(c => !c.Debug))
            terminal.WriteLine($"  {command.Usage,-40} {command.Description}");
    }

    private async Task ClearAsync(IReadOnlyList<string> arguments)
    {
        terminal.Clear();
    }

    private async Task RequestShutdownAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine("Shutting down...");
        hostRequests.Request(new HostRequest.Shutdown());
    }
}
