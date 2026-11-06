using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Simulation;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandContext
{
    public CommandContext(
        GameState gameState,
        GameLoop gameLoop,
        NetworkService networkService,
        ServerActions serverActions,
        HackingFormulas hackingFormulas,
        CommandRegistry commandRegistry,
        IGameTerminal terminal)
    {
        GameState = gameState;
        GameLoop = gameLoop;
        NetworkService = networkService;
        ServerActions = serverActions;
        HackingFormulas = hackingFormulas;
        CommandRegistry = commandRegistry;
        Terminal = terminal;
    }

    public GameState GameState { get; }
    public GameLoop GameLoop { get; }
    public NetworkService NetworkService { get; }
    public ServerActions ServerActions { get; }
    public HackingFormulas HackingFormulas { get; }
    public CommandRegistry CommandRegistry { get; }
    public IGameTerminal Terminal { get; }
}