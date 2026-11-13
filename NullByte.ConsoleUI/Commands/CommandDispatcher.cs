using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDispatcher
{
    private readonly CommandRegistry registry;
    private readonly IGameOutput output;

    public CommandDispatcher(CommandRegistry registry, IGameOutput output)
    {
        ArgumentNullException.ThrowIfNull(registry);

        this.registry = registry;
        this.output = output;
    }

    public async Task<bool> DispatchAsync(string commandLine)
    {
        var tokens = CommandLineParser.Parse(commandLine);
        if (tokens.Count == 0)
            return false;

        if (!registry.TryResolve(tokens, out var command, out var consumed))
        {
            output.WriteLine($"Unknown command: {tokens[0]}");
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