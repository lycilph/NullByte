using System.Diagnostics;
using NullByte.Core.Systems;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Simulation;

/// <summary>
/// Runs the core game simulation on a dedicated thread.
/// </summary>
public sealed class GameLoop : IAsyncDisposable, IGameThread
{
    // How long the game thread sleeps between ticks. A stop request wakes it immediately.
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(10);

    private readonly IGameClock clock;
    private readonly GameSynchronizationContext synchronizationContext;
    private readonly GameScheduler scheduler;

    private readonly CancellationTokenSource stopSource = new();
    private readonly TaskCompletionSource startedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stoppedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Set by Start() or by RequestStop() on a loop that never started: whichever comes first wins.
    private int startClaimed;
    private int disposed;

    private readonly List<ISimulationSystem> systems = [];
    private TimeSpan lastSystemUpdate;

    public GameLoop(IGameClock clock, GameSynchronizationContext synchronizationContext, GameScheduler scheduler)
    {
        this.clock = clock
            ?? throw new ArgumentNullException(nameof(clock));

        this.synchronizationContext = synchronizationContext
            ?? throw new ArgumentNullException(nameof(synchronizationContext));

        this.scheduler = scheduler;

        // Systems see the time that passes from now on, not the time before they existed.
        lastSystemUpdate = clock.Elapsed;
    }

    /// <summary>
    /// Raised on the game thread when posted work or a system throws. See
    /// <see cref="UnhandledException"/>. Only a failure of the loop
    /// itself (binding the thread, the scheduler) faults <see cref="Completion"/>.
    /// </summary>
    public event Action<Exception>? UnhandledException;

    public bool IsGameThread { get => synchronizationContext.IsGameThread; }

    /// <summary>
    /// Gets whether the loop has started and has not been asked to stop.
    /// </summary>
    public bool IsRunning
    {
        get => startedSource.Task.IsCompletedSuccessfully &&
               !stopSource.IsCancellationRequested &&
               !stoppedSource.Task.IsCompleted;
    }

    /// <summary>
    /// Completes when the game thread is initialized and ready.
    /// </summary>
    public Task Started => startedSource.Task;

    /// <summary>
    /// Completes when the game loop has stopped.
    /// </summary>
    public Task Completion => stoppedSource.Task;

    /// <summary>
    /// Registers a system that is updated on every tick. Must be called before <see cref="Start"/>.
    /// </summary>
    public void AddSystem(ISimulationSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        ObjectDisposedException.ThrowIf(disposed != 0, this);

        if (Volatile.Read(ref startClaimed) != 0)
            throw new InvalidOperationException("Systems can only be added before the game loop is started.");

        systems.Add(system);
    }

    /// <summary>
    /// Starts the game loop on a dedicated background thread.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);

        if (Interlocked.Exchange(ref startClaimed, 1) != 0)
            throw new InvalidOperationException("The game loop can only be started once.");

        new Thread(Run) { IsBackground = true, Name = "NullByte Game Thread" }.Start();
    }

    /// <summary>
    /// Asks the loop to stop. Safe to call at any time and more than once.
    /// </summary>
    public void RequestStop()
    {
        stopSource.Cancel();

        // Never started (and now never will): there is no thread to finish the job.
        if (Interlocked.Exchange(ref startClaimed, 1) == 0)
        {
            startedSource.TrySetCanceled();
            stoppedSource.TrySetResult();
        }
    }

    /// <summary>
    /// Posts work for asynchronous execution on the game thread.
    /// </summary>
    internal void Post(SendOrPostCallback callback, object? state = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(disposed != 0, this);

        if (!IsRunning)
            throw new InvalidOperationException("Work can only be posted while the game loop is running.");

        synchronizationContext.Post(callback, state);
    }

    /// <summary>
    /// Runs an asynchronous operation on the game thread and completes when it has finished.
    /// </summary>
    public Task InvokeAsync(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return InvokeAsync<object?>(async () =>
        {
            await callback();
            return null;
        });
    }

    /// <summary>
    /// Runs an asynchronous operation on the game thread and returns its result.
    /// </summary>
    public Task<T> InvokeAsync<T>(Func<Task<T>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(_ => _ = ExecuteAsync(callback, completion));

        return completion.Task;
    }

    private static async Task ExecuteAsync<T>(Func<Task<T>> callback, TaskCompletionSource<T> completion)
    {
        try
        {
            completion.TrySetResult(await callback());
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    public Task WaitForStopAsync(CancellationToken cancellationToken = default)
        => Completion.WaitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        RequestStop();

        try
        {
            await Completion.ConfigureAwait(false);
        }
        catch
        {
            // Disposal must still release owned resources.
        }

        stopSource.Dispose();
    }

    private void Run()
    {
        Exception? failure = null;

        try
        {
            synchronizationContext.BindToCurrentThread();
            clock.Start();
            startedSource.TrySetResult();

            while (!stopSource.IsCancellationRequested)
            {
                Tick();
                stopSource.Token.WaitHandle.WaitOne(TickInterval);
            }

            // Final tick, so work posted just before the stop request still runs.
            Tick();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            synchronizationContext.Complete();
            clock.Stop();
            synchronizationContext.Release();
        }

        if (failure is null)
        {
            stoppedSource.TrySetResult();
        }
        else
        {
            startedSource.TrySetException(failure);
            stoppedSource.TrySetException(failure);
        }
    }

    /// <summary>
    /// Runs one tick on the game thread.
    /// </summary>
    public void Tick()
    {
        // Timers first, so the continuations they release run in this tick's batch.
        scheduler.Update();
        UpdateSystems();
        DrainPendingWork();
    }

    // Every system sees the same delta, measured on the game clock, so speed
    // changes and a paused clock apply to all of them alike.
    private void UpdateSystems()
    {
        var now = clock.Elapsed;
        var delta = now - lastSystemUpdate;
        lastSystemUpdate = now;

        if (delta <= TimeSpan.Zero)
            return;

        foreach (var system in systems)
        {
            try
            {
                system.Update(delta);
            }
            catch (Exception exception)
            {
                ReportUnhandled(exception);
            }
        }
    }

    // One batch per tick: anything the callbacks post waits for the next tick.
    private void DrainPendingWork()
    {
        try
        {
            synchronizationContext.DrainBatch();
        }
        catch (AggregateException aggregate)
        {
            foreach (var exception in aggregate.InnerExceptions)
                ReportUnhandled(exception);
        }
    }

    private void ReportUnhandled(Exception exception)
    {
        try
        {
            if (UnhandledException is { } handler)
                handler(exception);
            else
                Trace.TraceError($"Unhandled exception on the game thread: {exception}");
        }
        catch
        {
            // A failing handler must not take down the simulation.
        }
    }
}