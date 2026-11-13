using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class SystemCommands(CommandRegistry registry, IGameTerminal terminal, ShutdownRequest? shutdownRequest) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
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
    }

    private async Task HelpAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine("Available commands:");
        foreach (var command in registry.Commands.Where(c => !c.Debug))
            terminal.WriteLine($"  {command.Usage,-22} {command.Description}");
    }

    private async Task ClearAsync(IReadOnlyList<string> arguments)
    {
        terminal.Clear();
    }

    private async Task RequestShutdownAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine("Shutting down...");
        shutdownRequest?.Request();
    }
}
