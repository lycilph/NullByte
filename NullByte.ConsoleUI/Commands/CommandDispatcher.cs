using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;
using NullByte.Core.Narrative;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDispatcher
{
    private readonly CommandRegistry registry;
    private readonly IGameOutput output;
    private readonly NarrativeCoordinator? narrative;

    public CommandDispatcher(CommandRegistry registry, IGameOutput output, NarrativeCoordinator? narrative = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        this.registry = registry;
        this.output = output;
        this.narrative = narrative;
    }

    public async Task<bool> DispatchAsync(string commandLine)
    {
        var tokens = CommandLineParser.Parse(commandLine);
        if (tokens.Count == 0)
            return false;

        if (!registry.TryResolve(tokens, out var command, out var consumed))
        {
            output.WriteLine($"Unknown command: {tokens[0]}");
            if (registry.TryGet("help", out _))
                output.WriteLine("Type 'help' to see available commands.");
            return false;
        }

        var args = tokens.Skip(consumed).ToArray();
        if (args.Length < command.MinArgs || args.Length > command.MaxArgs)
        {
            WriteUsage(command);
            return false;
        }

        await command.Handler(args);
        if (command.Name.Equals("status", StringComparison.OrdinalIgnoreCase))
            narrative?.StatusUsed();
        return true;
    }

    private void WriteUsage(CommandDefinition command)
    {
        output.WriteLine($"Usage: {command.Usage}");

        // "botnet foo" lands on "botnet" with a stray argument: show the subcommands too.
        foreach (var sub in registry.Subcommands(command))
            output.WriteLine($"       {sub.Usage}");
    }
}