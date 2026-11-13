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
        ArgumentNullException.ThrowIfNull(commandLine);

        var arguments = CommandLineParser.Parse(commandLine);

        if (arguments.Count == 0)
            return false;

        var commandName = arguments[0];

        if (!registry.TryGet(commandName, out var command))
        {
            output.WriteLine($"Unknown command: {commandName}");
            output.WriteLine("Type 'help' to see available commands.");

            return false;
        }

        var commandArguments = arguments.Skip(1).ToArray();
        if (commandArguments.Length < command!.MinArgs || commandArguments.Length > command!.MaxArgs)
        {
            output.WriteLine($"Usage: {command!.Usage}");
            return false;
        }

        await command!.Handler(commandArguments);

        return true;
    }
}