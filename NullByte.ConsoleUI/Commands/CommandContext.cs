using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Simulation;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandContext(
    GameState gameState,
    GameLoop gameLoop,
    NetworkService networkService,
    ServerActions serverActions,
    HackingFormulas hackingFormulas,
    CommandRegistry commandRegistry,
    IGameTerminal terminal)
{
    public GameState GameState { get; } = gameState;
    public GameLoop GameLoop { get; } = gameLoop;
    public NetworkService NetworkService { get; } = networkService;
    public ServerActions ServerActions { get; } = serverActions;
    public HackingFormulas HackingFormulas { get; } = hackingFormulas;
    public CommandRegistry CommandRegistry { get; } = commandRegistry;
    public IGameTerminal Terminal { get; } = terminal;
}