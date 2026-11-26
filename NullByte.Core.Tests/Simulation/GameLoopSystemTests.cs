using NullByte.Core.Systems;
using NullByte.Core.Tests.Infrastructure;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Simulation;

/// <summary>
/// Covers how the game loop drives <see cref="ISimulationSystem"/>s: on the
/// game thread, in order, with positive deltas, and without letting one
/// failing system stop the game.
/// </summary>
public sealed class GameLoopSystemTests
{
    [Fact]
    public async Task AddSystem_AfterStart_Throws()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var exception = Assert.Throws<InvalidOperationException>(
            () => host.Loop.AddSystem(new RecordingSystem(_ => { })));

        Assert.Contains("before", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSystem_WithNull_Throws()
    {
        await using var host = new GameLoopHost();

        Assert.Throws<ArgumentNullException>(() => host.Loop.AddSystem(null!));
    }

    [Fact]
    public async Task Systems_AreUpdatedOnTheGameThreadWithPositiveDeltas()
    {
        await using var host = new GameLoopHost();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deltas = new List<TimeSpan>();
        var allOnGameThread = true;

        host.Loop.AddSystem(new RecordingSystem(delta =>
        {
            allOnGameThread &= host.Loop.IsGameThread;
            deltas.Add(delta);

            if (deltas.Count == 20)
                done.TrySetResult();
        }));

        await host.StartAsync();
        await GameLoopHost.WithinTimeout(done.Task);
        await host.StopAsync();

        Assert.True(allOnGameThread);
        Assert.All(deltas, delta => Assert.True(delta > TimeSpan.Zero));
    }

    [Fact]
    public async Task Systems_SeeTheGameTimeThatPassed()
    {
        await using var host = new GameLoopHost();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var total = TimeSpan.Zero;

        host.Loop.AddSystem(new RecordingSystem(delta =>
        {
            total += delta;

            if (total >= TimeSpan.FromMilliseconds(100))
                done.TrySetResult();
        }));

        await host.StartAsync();
        await GameLoopHost.WithinTimeout(done.Task);
        await host.StopAsync();

        // The deltas tile the game clock without gaps, so they cannot add up to more than it shows.
        Assert.True(total <= host.Clock.Elapsed);
    }

    [Fact]
    public async Task Systems_AreUpdatedInTheOrderTheyWereAdded()
    {
        await using var host = new GameLoopHost();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();

        host.Loop.AddSystem(new RecordingSystem(_ => order.Add("first")));
        host.Loop.AddSystem(new RecordingSystem(_ =>
        {
            order.Add("second");

            if (order.Count >= 10)
                done.TrySetResult();
        }));

        await host.StartAsync();
        await GameLoopHost.WithinTimeout(done.Task);
        await host.StopAsync();

        for (var i = 0; i + 1 < order.Count; i += 2)
        {
            Assert.Equal("first", order[i]);
            Assert.Equal("second", order[i + 1]);
        }
    }

    [Fact]
    public async Task FailingSystem_WithHandler_IsReportedAndTheOthersKeepRunning()
    {
        await using var host = new GameLoopHost();
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var healthyUpdates = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;

        host.Loop.UnhandledException += exception => reported.TrySetResult(exception);

        host.Loop.AddSystem(new RecordingSystem(_ => throw new InvalidOperationException("System crashed.")));
        host.Loop.AddSystem(new RecordingSystem(_ =>
        {
            if (++count == 5)
                healthyUpdates.TrySetResult();
        }));

        host.Loop.Start();

        var exception = await GameLoopHost.WithinTimeout(reported.Task);
        await GameLoopHost.WithinTimeout(healthyUpdates.Task);

        Assert.Equal("System crashed.", exception.Message);
        Assert.True(host.Loop.IsRunning);
    }

    [Fact]
    public async Task BotnetSystem_InTheRunningLoop_PaysThePlayerAsGameTimePasses()
    {
        // At 100x speed one real second is 100 game seconds, so a level one node makes about 150 per real second.
        await using var host = new GameLoopHost(new RealGameClock { SpeedMultiplier = 100 });

        var world = CoreTestSetup.CreateWorld();
        world.State.Botnet.AddNode();

        host.Loop.AddSystem(world.BotnetSystem);
        await host.StartAsync();

        var deadline = DateTime.UtcNow + GameLoopHost.Timeout;
        var money = 0.0;

        while (money < 150 && DateTime.UtcNow < deadline)
        {
            // Game state is only read on the game thread.
            money = await host.Loop.InvokeAsync(() => Task.FromResult(world.Player.Money));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var earned = await host.Loop.InvokeAsync(() => Task.FromResult(world.State.Botnet.TotalEarned));

        Assert.True(money >= 150, $"The botnet only paid {money:N2} within {GameLoopHost.Timeout.TotalSeconds:N0} seconds.");
        Assert.True(earned > 0);
    }

    private sealed class RecordingSystem(Action<TimeSpan> onUpdate) : ISimulationSystem
    {
        public void Update(TimeSpan delta) => onUpdate(delta);
    }
}
