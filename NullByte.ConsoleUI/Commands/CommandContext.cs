using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Simulation;
using NullByte.Core.Time;
using NullByte.Scripting.Runtime;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandContext(
    GameState gameState,
    GameLoop gameLoop,
    IGameClock clock,
    NetworkService networkService,
    ServerActions serverActions,
    BotnetService botnetService,
    ScriptService scriptService,
    HackingFormulas hackingFormulas,
    BotnetFormulas botnetFormulas,
    CommandRegistry commandRegistry,
    IGameTerminal terminal)
{
    public GameState GameState { get; } = gameState;
    public GameLoop GameLoop { get; } = gameLoop;
    public IGameClock Clock { get; } = clock;
    public NetworkService NetworkService { get; } = networkService;
    public ServerActions ServerActions { get; } = serverActions;
    public BotnetService BotnetService { get; } = botnetService;
    public ScriptService ScriptService { get; } = scriptService;
    public HackingFormulas HackingFormulas { get; } = hackingFormulas;
    public BotnetFormulas BotnetFormulas { get; } = botnetFormulas;
    public CommandRegistry CommandRegistry { get; } = commandRegistry;
    public IGameTerminal Terminal { get; } = terminal;
}