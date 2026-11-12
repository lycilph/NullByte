using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Simulation;
using NullByte.Core.Systems;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Simulation;

/// <summary>
/// Covers how the game loop drives <see cref="ISimulationSystem"/>s: on the
/// game thread, in order, with positive deltas, and without letting one
/// failing system stop the game.
/// </summary>
public sealed class GameLoopSystemTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task AddSystem_AfterStart_Throws()
    {
        await using var gameLoop = CreateGameLoop(new RealGameClock());

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        var exception = Assert.Throws<InvalidOperationException>(
            () => gameLoop.AddSystem(new RecordingSystem(_ => { })));

        Assert.Contains("before", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddSystem_WithNull_Throws()
    {
        var gameLoop = CreateGameLoop(new RealGameClock());

        Assert.Throws<ArgumentNullException>(() => gameLoop.AddSystem(null!));
    }

    [Fact]
    public async Task Systems_AreUpdatedOnTheGameThreadWithPositiveDeltas()
    {
        await using var gameLoop = CreateGameLoop(new RealGameClock());

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deltas = new List<TimeSpan>();
        var allOnGameThread = true;

        gameLoop.AddSystem(new RecordingSystem(delta =>
        {
            allOnGameThread &= gameLoop.IsGameThread;
            deltas.Add(delta);

            if (deltas.Count == 20)
                done.TrySetResult();
        }));

        gameLoop.Start();
        await done.Task.WaitAsync(Timeout);

        gameLoop.RequestStop();
        await gameLoop.Completion.WaitAsync(Timeout);

        Assert.True(allOnGameThread);
        Assert.All(deltas, delta => Assert.True(delta > TimeSpan.Zero));
    }

    [Fact]
    public async Task Systems_SeeTheGameTimeThatPassed()
    {
        var clock = new RealGameClock();
        await using var gameLoop = CreateGameLoop(clock);

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var total = TimeSpan.Zero;

        gameLoop.AddSystem(new RecordingSystem(delta =>
        {
            total += delta;

            if (total >= TimeSpan.FromMilliseconds(100))
                done.TrySetResult();
        }));

        gameLoop.Start();
        await done.Task.WaitAsync(Timeout);

        gameLoop.RequestStop();
        await gameLoop.Completion.WaitAsync(Timeout);

        // The deltas tile the game clock without gaps, so they cannot add up to more than it shows.
        Assert.True(total <= clock.Elapsed);
    }

    [Fact]
    public async Task Systems_AreUpdatedInTheOrderTheyWereAdded()
    {
        await using var gameLoop = CreateGameLoop(new RealGameClock());

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();

        gameLoop.AddSystem(new RecordingSystem(_ => order.Add("first")));
        gameLoop.AddSystem(new RecordingSystem(_ =>
        {
            order.Add("second");

            if (order.Count >= 10)
                done.TrySetResult();
        }));

        gameLoop.Start();
        await done.Task.WaitAsync(Timeout);

        gameLoop.RequestStop();
        await gameLoop.Completion.WaitAsync(Timeout);

        for (var i = 0; i + 1 < order.Count; i += 2)
        {
            Assert.Equal("first", order[i]);
            Assert.Equal("second", order[i + 1]);
        }
    }

    [Fact]
    public async Task FailingSystem_WithHandler_IsReportedAndTheOthersKeepRunning()
    {
        await using var gameLoop = CreateGameLoop(new RealGameClock());

        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var healthyUpdates = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;

        gameLoop.UnhandledException += exception => reported.TrySetResult(exception);

        gameLoop.AddSystem(new RecordingSystem(_ => throw new InvalidOperationException("System crashed.")));
        gameLoop.AddSystem(new RecordingSystem(_ =>
        {
            if (++count == 5)
                healthyUpdates.TrySetResult();
        }));

        gameLoop.Start();

        var exception = await reported.Task.WaitAsync(Timeout);
        await healthyUpdates.Task.WaitAsync(Timeout);

        Assert.Equal("System crashed.", exception.Message);
        Assert.True(gameLoop.IsRunning);
    }

    [Fact]
    public async Task BotnetSystem_InTheRunningLoop_PaysThePlayerAsGameTimePasses()
    {
        // At 100x speed one real second is 100 game seconds, so a level one node makes about 150 per real second.
        var clock = new RealGameClock { SpeedMultiplier = 100 };
        await using var gameLoop = new GameLoop(clock, new GameSynchronizationContext());

        var home = new Server("home", 0, 0, 5, 1, 1, 1, 0, 8, hasRootAccess: true);
        var network = new Network();
        network.AddServer(home);

        var gameState = new GameState(new Player("home"), network);
        gameState.Botnet.AddNode();

        gameLoop.AddSystem(new BotnetSystem(gameState, new BotnetFormulas(new BalanceConfig())));
        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        var deadline = DateTime.UtcNow + Timeout;
        var money = 0.0;

        while (money < 150 && DateTime.UtcNow < deadline)
        {
            // Game state is only read on the game thread.
            money = await gameLoop.InvokeAsync(() => Task.FromResult(gameState.Player.Money));
            await Task.Delay(10);
        }

        var earned = await gameLoop.InvokeAsync(() => Task.FromResult(gameState.Botnet.TotalEarned));

        Assert.True(money >= 150, $"The botnet only paid {money:N2} within {Timeout.TotalSeconds:N0} seconds.");
        Assert.True(earned > 0);
    }

    private static GameLoop CreateGameLoop(IGameClock clock)
        => new(clock, new GameSynchronizationContext());

    private sealed class RecordingSystem(Action<TimeSpan> onUpdate) : ISimulationSystem
    {
        public void Update(TimeSpan delta) => onUpdate(delta);
    }
}