Now that every M3 command works, we can refactor the large switch without changing behavior. I recommend keeping the existing CommandLineParser and introducing a lightweight registry with delegate-based handlers, rather than creating one class per command.

Target structure
Plain Text
NullByte.ConsoleUI/
Commands/
CommandContext.cs
CommandDefinition.cs
CommandRegistry.cs
CommandDispatcher.cs

The responsibilities should be:

CommandLineParser: converts a command line into arguments.
CommandDefinition: describes one command and its handler.
CommandRegistry: owns and looks up registered commands.
CommandDispatcher: parses and executes a command line.
CommandContext: supplies shared game services to handlers.

This lets us remove the switch while keeping the existing command methods mostly unchanged.

1. Add CommandContext.cs
1. using NullByte.Core.Actions;
using NullByte.Core.Domain;
using NullByte.Core.Network;
using NullByte.Core.Simulation;
using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandContext
{
    public CommandContext(
        GameState gameState,
        GameLoop gameLoop,
        NetworkService networkService,
        ServerActions serverActions,
        IGameTerminal terminal)
    {
        GameState = gameState;
        GameLoop = gameLoop;
        NetworkService = networkService;
        ServerActions = serverActions;
        Terminal = terminal;
    }

    public GameState GameState { get; }

    public GameLoop GameLoop { get; }

    public NetworkService NetworkService { get; }

    public ServerActions ServerActions { get; }

    public IGameTerminal Terminal { get; }
}

Adjust the namespaces to match the current solution.

This is preferable to passing four or five dependencies into every individual command handler. It is also a natural seam for console-level tests later.

I would pass the full GameState here because several commands need different parts of it:

money and status need the player.
scan, connect, and home use NetworkService.
Manual actions use ServerActions.
Diagnostic or progress operations may need GameLoop.

The context belongs to the application layer. It should not move into NullByte.Core.

2. Add CommandDefinition.cs
1. namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDefinition
{
    public CommandDefinition(
        string name,
        string usage,
        string description,
        Func<CommandContext, IReadOnlyList<string>, Task> handler,
        params string[] aliases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(usage);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(aliases);

        Name = name;
        Usage = usage;
        Description = description;
        Handler = handler;
        Aliases = aliases;
    }

    public string Name { get; }

    public string Usage { get; }

    public string Description { get; }

    public Func<CommandContext, IReadOnlyList<string>, Task> Handler { get; }

    public IReadOnlyList<string> Aliases { get; }
}

The handler receives arguments excluding the command name. For example:

Plain Text
connect n00dles

becomes:

C#
["n00dles"]

That makes each handler easier to reason about:

C#
if (arguments.Count != 1)
{
terminal.WriteLine("Usage: connect <hostname>");
return;
}
 
var hostname = arguments[0];

3. Add CommandRegistry.cs
1. namespace NullByte.ConsoleUI.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandDefinition> commands =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CommandDefinition> Commands =>
        commands.Values
            .Distinct()
            .OrderBy(command => command.Name)
            .ToArray();

    public void Register(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        RegisterName(command.Name, command);

        foreach (var alias in command.Aliases)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(alias);
            RegisterName(alias, command);
        }
    }

    public bool TryGet(
        string name,
        out CommandDefinition? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return commands.TryGetValue(name, out command);
    }

    private void RegisterName(
        string name,
        CommandDefinition command)
    {
        if (!commands.TryAdd(name, command))
        {
            throw new InvalidOperationException(
                $"A command named '{name}' is already registered.");
        }
    }
}

There is one small issue with the Commands property: commands with aliases appear multiple times in the dictionary. Distinct() works because every alias references the same CommandDefinition instance.

A more explicit implementation would maintain a second collection of canonical definitions, but I do not think that is necessary yet.

4. Add CommandDispatcher.cs
 namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDispatcher
{
    private readonly CommandRegistry registry;

    public CommandDispatcher(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        this.registry = registry;
    }

    public async Task<bool> DispatchAsync(
        string commandLine,
        CommandContext context)
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

        var commandArguments =
            arguments.Skip(1).ToArray();

        await command.Handler(
            context,
            commandArguments);

        return true;
    }
}

I would have DispatchAsync return bool:

true: a registered command was executed.
false: input was empty or the command was unknown.

We may not need the return value immediately, but it is useful in tests and costs almost nothing.

5. Keep exit outside the registry initially

exit controls the outer command-reading loop rather than game state. I would keep it in ProcessCommandsAsync:

private static async Task ProcessCommandsAsync(
    GameLoop gameLoop,
    CommandDispatcher dispatcher,
    CommandContext context,
    ThreadSafeConsoleTerminal terminal)
{
    await foreach (var commandLine in terminal.ReadCommandsAsync())
    {
        if (IsExitCommand(commandLine))
        {
            terminal.WriteLine("Shutting down...");
            return;
        }

        try
        {
            await gameLoop.InvokeAsync(
                () => dispatcher.DispatchAsync(
                    commandLine,
                    context));
        }
        catch (OperationCanceledException)
            when (terminal.StoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (InvalidOperationException)
            when (!gameLoop.IsRunning)
        {
            return;
        }
        catch (Exception exception)
        {
            terminal.WriteLine(
                $"Command failed: {exception.Message}");
        }
    }
}

private static bool IsExitCommand(string commandLine)
{
    var arguments = CommandLineParser.Parse(commandLine);

    if (arguments.Count != 1)
        return false;

    return arguments[0].Equals(
        "exit",
        StringComparison.OrdinalIgnoreCase) ||
        arguments[0].Equals(
            "quit",
            StringComparison.OrdinalIgnoreCase);
}

Eventually exit could return a structured outcome such as:

C#
CommandResult.ExitRequested

But that is more machinery than M3 needs.

6. Register the commands in one place

Add a factory method in Program.cs first:
private static CommandRegistry CreateCommandRegistry()
{
    var registry = new CommandRegistry();

    registry.Register(
        new CommandDefinition(
            "help",
            "help",
            "Show available commands.",
            HelpAsync));

    registry.Register(
        new CommandDefinition(
            "scan",
            "scan",
            "Scan adjacent servers.",
            ScanAsync));

    registry.Register(
        new CommandDefinition(
            "connect",
            "connect <hostname>",
            "Connect to an adjacent server.",
            ConnectAsync));

    registry.Register(
        new CommandDefinition(
            "home",
            "home",
            "Return to the home server.",
            HomeAsync));

    registry.Register(
        new CommandDefinition(
            "analyze",
            "analyze [hostname]",
            "Show detailed server information.",
            AnalyzeAsync));

    registry.Register(
        new CommandDefinition(
            "nuke",
            "nuke",
            "Gain root access to the current server.",
            NukeAsync));

    registry.Register(
        new CommandDefinition(
            "hack",
            "hack",
            "Hack the current server.",
            HackAsync));

    registry.Register(
        new CommandDefinition(
            "grow",
            "grow",
            "Increase money on the current server.",
            GrowAsync));

    registry.Register(
        new CommandDefinition(
            "weaken",
            "weaken",
            "Reduce security on the current server.",
            WeakenAsync));

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
            "Show the player's current status.",
            StatusAsync));

    registry.Register(
        new CommandDefinition(
            "ls",
            "ls",
            "List files on the current server.",
            ListFilesAsync));

    registry.Register(
        new CommandDefinition(
            "clear",
            "clear",
            "Clear the terminal.",
            ClearAsync,
            "cls"));

    return registry;
}

The aliases system gives us cls without special dispatcher logic.

7. Adapt existing methods incrementally

Do not rewrite every command at once. Start with the simplest command:

private static Task MoneyAsync(
    CommandContext context,
    IReadOnlyList<string> arguments)
{
    if (arguments.Count != 0)
    {
        context.Terminal.WriteLine(
            "Usage: money");

        return Task.CompletedTask;
    }

    context.Terminal.WriteLine(
        $"Money: ${context.GameState.Player.Money:N2}");

    return Task.CompletedTask;
}

An asynchronous handler returning Task.CompletedTask is fine. It gives all commands one uniform signature without adding fake async modifiers.

A manual operation remains naturally asynchronous:

private static async Task HackAsync(
    CommandContext context,
    IReadOnlyList<string> arguments)
{
    if (arguments.Count != 0)
    {
        context.Terminal.WriteLine(
            "Usage: hack");

        return;
    }

    var server =
        context.NetworkService.GetCurrentServer();

    await RunWithInputBlockedAsync(
        context.Terminal,
        () => RunHackAsync(
            server,
            context.ServerActions,
            context.Terminal));
}

You can initially wrap existing methods from these handlers, then simplify their parameter lists afterward.

8. Generate help from the registry

This is the main practical benefit of storing usage and descriptions in CommandDefinition. Help no longer has to duplicate the command list.

The registry can be included in CommandContext:

public CommandContext(
    GameState gameState,
    GameLoop gameLoop,
    NetworkService networkService,
    ServerActions serverActions,
    CommandRegistry commandRegistry,
    IGameTerminal terminal)
{
    GameState = gameState;
    GameLoop = gameLoop;
    NetworkService = networkService;
    ServerActions = serverActions;
    CommandRegistry = commandRegistry;
    Terminal = terminal;
}

public CommandRegistry CommandRegistry { get; }

Then:

private static Task HelpAsync(
    CommandContext context,
    IReadOnlyList<string> arguments)
{
    if (arguments.Count != 0)
    {
        context.Terminal.WriteLine(
            "Usage: help");

        return Task.CompletedTask;
    }

    context.Terminal.WriteLine(
        "Available commands:");

    foreach (var command in context.CommandRegistry.Commands)
    {
        context.Terminal.WriteLine(
            $"  {command.Usage,-22} {command.Description}");
    }

    context.Terminal.WriteLine(
        "  exit                   Exit NullByte");

    return Task.CompletedTask;
}

This ensures help stays synchronized with registration.

Recommended implementation sequence

I would proceed in these small steps:

Add CommandContext.
Add CommandDefinition.
Add CommandRegistry and its tests.
Add CommandDispatcher and its tests.
Register only money and verify the path end to end.
Move the remaining commands one at a time.
Generate help from the registry.
Delete the old command switch.

The next concrete file should be CommandDefinition.cs, followed by CommandRegistry.cs. I would test the registry before integrating it into Program.cs, because duplicate names, aliases, and case-insensitive lookup are the key invariants worth locking down.