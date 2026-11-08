using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Services;
using NullByte.Core.Simulation;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.ConsoleUI.Tests;

public sealed class BotnetCommandTests
{
    [Fact]
    public void BotnetCommand_IsRegistered()
    {
        var registry = CommandRegistryFactory.Create();

        Assert.True(registry.TryGet("botnet", out var command));
        Assert.NotNull(command);
        Assert.False(command!.Debug);
    }

    [Theory]
    [InlineData("botnet")]
    [InlineData("botnet status")]
    public async Task Status_WithoutNodes_ShowsAnEmptyBotnet(string commandLine)
    {
        var harness = new Harness(money: 0);

        await harness.RunAsync(commandLine);

        Assert.Contains("Nodes: 0", harness.Output);
        Assert.Contains("Next node costs", harness.Output);
    }

    [Fact]
    public async Task Buy_WithEnoughMoney_BuysTheNode()
    {
        var harness = new Harness(money: 1_500);

        await harness.RunAsync("botnet buy");

        Assert.Equal(1, harness.GameState.Botnet.NodeCount);
        Assert.Equal(500.0, harness.GameState.Player.Money);
        Assert.Contains("Bought botnet node #1", harness.Output);
    }

    [Fact]
    public async Task Buy_WithTooLittleMoney_SaysSoAndBuysNothing()
    {
        var harness = new Harness(money: 10);

        await harness.RunAsync("botnet buy");

        Assert.Equal(0, harness.GameState.Botnet.NodeCount);
        Assert.Equal(10.0, harness.GameState.Player.Money);
        Assert.Contains("Not enough money", harness.Output);
    }

    [Theory]
    [InlineData("level", 2, 1, 1)]
    [InlineData("ram", 1, 2, 1)]
    [InlineData("core", 1, 1, 2)]
    [InlineData("cores", 1, 1, 2)]
    [InlineData("LEVEL", 2, 1, 1)]
    public async Task Upgrade_UsesNodeNumbersCountedFromOne(string kind, int level, int ram, int cores)
    {
        var harness = new Harness(money: 10_000_000);
        harness.GameState.Botnet.AddNode();
        harness.GameState.Botnet.AddNode();

        await harness.RunAsync($"botnet upgrade 2 {kind}");

        var untouched = harness.GameState.Botnet.Nodes[0];
        var upgraded = harness.GameState.Botnet.Nodes[1];

        Assert.Equal(1, untouched.Level);
        Assert.Equal(level, upgraded.Level);
        Assert.Equal(ram, upgraded.Ram);
        Assert.Equal(cores, upgraded.Cores);
        Assert.Contains("Upgraded node #2", harness.Output);
    }

    [Theory]
    [InlineData("botnet upgrade 9 level")]
    [InlineData("botnet upgrade 0 level")]
    public async Task Upgrade_OfAMissingNode_SaysSo(string commandLine)
    {
        var harness = new Harness(money: 10_000_000);
        harness.GameState.Botnet.AddNode();

        await harness.RunAsync(commandLine);

        Assert.Contains("no such botnet node", harness.Output);
        Assert.Equal(10_000_000.0, harness.GameState.Player.Money);
    }

    [Theory]
    [InlineData("botnet upgrade")]
    [InlineData("botnet upgrade 1")]
    [InlineData("botnet upgrade one level")]
    [InlineData("botnet upgrade 1 speed")]
    [InlineData("botnet upgrade 1 level extra")]
    [InlineData("botnet buy now")]
    [InlineData("botnet status extra")]
    [InlineData("botnet frobnicate")]
    public async Task InvalidInput_ShowsUsageAndChangesNothing(string commandLine)
    {
        var harness = new Harness(money: 10_000_000);
        harness.GameState.Botnet.AddNode();

        await harness.RunAsync(commandLine);

        Assert.Contains("Usage:", harness.Output);
        Assert.Equal(10_000_000.0, harness.GameState.Player.Money);
        Assert.Equal(1, harness.GameState.Botnet.NodeCount);
        Assert.Equal(1, harness.GameState.Botnet.Nodes[0].Level);
    }

    [Fact]
    public async Task Status_ListsEachNodeAndMarksMaxedUpgrades()
    {
        var harness = new Harness(
            money: 0,
            new BalanceConfig { BotnetMaxLevel = 1 });
        harness.GameState.Botnet.AddNode();
        harness.GameState.Botnet.AddNode();

        await harness.RunAsync("botnet status");

        Assert.Contains("Nodes: 2", harness.Output);
        Assert.Contains("max", harness.Output);
    }

    private sealed class Harness
    {
        private readonly CommandDispatcher dispatcher;
        private readonly CommandContext context;
        private readonly FakeTerminal terminal = new();

        public Harness(double money, BalanceConfig? balance = null)
        {
            balance ??= new BalanceConfig();

            var home = new Server(
                id: "home",
                hostname: "home",
                maximumMoney: 0,
                startingMoney: 0,
                minimumSecurity: 1,
                startingSecurity: 1,
                requiredHackingLevel: 1,
                growthParameter: 0,
                maximumRam: 8,
                hasRootAccess: true);

            var network = new Network();
            network.AddServer(home);

            GameState = new GameState(new Player("home", startingMoney: money), network);

            var clock = new RealGameClock();
            var gameLoop = new GameLoop(clock, new GameSynchronizationContext());
            var hackingFormulas = new HackingFormulas(balance);
            var botnetFormulas = new BotnetFormulas(balance);
            var registry = CommandRegistryFactory.Create();

            dispatcher = new CommandDispatcher(registry);
            context = new CommandContext(
                GameState,
                gameLoop,
                new NetworkService(GameState),
                new ServerActions(GameState, gameLoop.Scheduler, hackingFormulas, new SeededRandomSource(1)),
                new BotnetService(GameState, botnetFormulas),
                hackingFormulas,
                botnetFormulas,
                registry,
                terminal);
        }

        public GameState GameState { get; }

        public string Output => string.Join(Environment.NewLine, terminal.Lines);

        public Task RunAsync(string commandLine) => dispatcher.DispatchAsync(commandLine, context);
    }

    private sealed class FakeTerminal : IGameTerminal
    {
        public List<string> Lines { get; } = [];

        public CancellationToken StoppingToken => CancellationToken.None;

        public void StartProgress(string msg, Func<double> progress) { }
        public void StopProgress() { }
        public void SetPrompt(string prompt) { }
        public void SetInputEnabled(bool enabled) { }

        public async IAsyncEnumerable<string> ReadCommandsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public void WriteLine() => Lines.Add(string.Empty);
        public void WriteLine(string message) => Lines.Add(message);
        public void Clear() => Lines.Clear();
    }
}