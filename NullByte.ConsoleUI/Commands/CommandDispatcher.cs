using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDispatcher
{
    private readonly CommandRegistry registry;

    public CommandDispatcher(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        this.registry = registry;
    }

    public async Task<bool> DispatchAsync(string commandLine, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(context);

        var arguments = CommandLineParser.Parse(commandLine);

        if (arguments.Count == 0)
            return false;

        var commandName = arguments[0];

        if (!registry.TryGet(commandName, out var command))
        {
            context.Terminal.WriteLine(
                $"Unknown command: {commandName}");

            context.Terminal.WriteLine(
                "Type 'help' to see available commands.");

            return false;
        }

        var commandArguments = arguments.Skip(1).ToArray();

        await command!.Handler(context, commandArguments);

        return true;
    }
}