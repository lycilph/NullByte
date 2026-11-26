using NullByte.Core.Simulation;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Infrastructure;

/// <summary>
/// A <see cref="GameLoop"/> on a real thread and (by default) the real clock, with the start/stop/timeout
/// plumbing every loop test needs. The loop's own behavior is what those tests cover, so it is not faked.
/// </summary>
internal sealed class GameLoopHost : IAsyncDisposable
{
    /// <summary>How long a test waits for the loop before it fails instead of hanging.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public GameLoopHost(IGameClock? clock = null, GameSynchronizationContext? context = null)
    {
        Clock = clock ?? new RealGameClock();
        Context = context ?? new GameSynchronizationContext();
        Scheduler = new GameScheduler(Clock);
        Loop = new GameLoop(Clock, Context, Scheduler);
    }

    public IGameClock Clock { get; }
    public GameSynchronizationContext Context { get; }
    public GameScheduler Scheduler { get; }
    public GameLoop Loop { get; }

    /// <summary>Starts the loop and waits until its game thread is ready.</summary>
    public async Task StartAsync()
    {
        Loop.Start();

        await Loop.Started.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    /// <summary>Asks the loop to stop and waits until it has.</summary>
    public async Task StopAsync()
    {
        Loop.RequestStop();

        await Loop.Completion.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    /// <summary>Waits for <paramref name="task"/>, failing instead of hanging if it takes longer than <see cref="Timeout"/>.</summary>
    public static Task WithinTimeout(Task task)
        => task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

    /// <inheritdoc cref="WithinTimeout(Task)"/>
    public static Task<T> WithinTimeout<T>(Task<T> task)
        => task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => Loop.DisposeAsync();
}
