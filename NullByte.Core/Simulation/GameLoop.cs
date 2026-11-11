using System.Diagnostics;
using NullByte.Core.Systems;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Simulation;

/// <summary>
/// Runs the core game simulation on a dedicated thread.
/// </summary>
public sealed class GameLoop : IAsyncDisposable
{
    // How long the game thread sleeps between ticks. A stop request wakes it immediately.
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(10);

    private readonly IGameClock clock;
    private readonly GameSynchronizationContext synchronizationContext;

    private readonly CancellationTokenSource stopSource = new();
    private readonly TaskCompletionSource startedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stoppedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Set by Start() or by RequestStop() on a loop that never started: whichever comes first wins.
    private int startClaimed;
    private int disposed;

    private readonly List<ISimulationSystem> systems = [];
    private TimeSpan lastSystemUpdate;

    public GameLoop(IGameClock clock, GameSynchronizationContext synchronizationContext)
    {
        this.clock = clock
            ?? throw new ArgumentNullException(nameof(clock));

        this.synchronizationContext = synchronizationContext
            ?? throw new ArgumentNullException(nameof(synchronizationContext));

        Scheduler = new GameScheduler(clock);

        // Systems see the time that passes from now on, not the time before they existed.
        lastSystemUpdate = clock.Elapsed;
    }

    public GameScheduler Scheduler { get; }

    /// <summary>
    /// Raised on the game thread when posted work or a system throws. See
    /// <see cref="GameSimulation.UnhandledException"/>. Only a failure of the loop
    /// itself (binding the thread, the scheduler) faults <see cref="Completion"/>.
    /// </summary>
    public event Action<Exception>? UnhandledException;

    public bool IsGameThread => synchronizationContext.IsGameThread;
    /// <summary>
    /// Gets whether the loop has started and has not been asked to stop.
    /// </summary>
    public bool IsRunning =>
        startedSource.Task.IsCompletedSuccessfully &&
        !stopSource.IsCancellationRequested &&
        !stoppedSource.Task.IsCompleted;

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
        Scheduler.Update();
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

    //private const int DefaultIdleDelayMilliseconds = 20;

    //private readonly IGameClock clock;
    //private readonly GameSynchronizationContext synchronizationContext;
    //private readonly GameScheduler scheduler;
    //private readonly CancellationTokenSource stopSource = new();
    //private readonly TaskCompletionSource startedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    //private readonly TaskCompletionSource stoppedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    //private readonly Lock lifecycleLock = new();
    //private readonly List<ISimulationSystem> systems = [];

    //private GameLoopState state = GameLoopState.Created;
    //private Thread? gameThread;
    //private TimeSpan lastSystemUpdate;

    //public GameLoop(IGameClock clock, GameSynchronizationContext synchronizationContext)
    //{
    //    this.clock = clock
    //        ?? throw new ArgumentNullException(nameof(clock));

    //    this.synchronizationContext = synchronizationContext
    //        ?? throw new ArgumentNullException(
    //            nameof(synchronizationContext));

    //    scheduler = new GameScheduler(clock);
    //}

    ///// <summary>
    ///// Raised on the game thread when work posted to the game thread throws.
    ///// </summary>
    ///// <remarks>
    ///// With no subscriber, such a failure is fatal: the loop faults and
    ///// <see cref="Completion"/> fails. With a subscriber, each exception is
    ///// reported and the loop keeps running, which is what scripts need: a
    ///// crashing script must not take the game down. A handler that throws is
    ///// ignored.
    ///// </remarks>
    //public event Action<Exception>? UnhandledException;

    ///// <summary>
    ///// Gets whether the game loop is currently running.
    ///// </summary>
    //public bool IsRunning
    //{
    //    get
    //    {
    //        lock (lifecycleLock)
    //        {
    //            return state == GameLoopState.Running;
    //        }
    //    }
    //}

    ///// <summary>
    ///// Gets whether the calling thread is the game thread.
    ///// </summary>
    //public bool IsGameThread => synchronizationContext.IsGameThread;

    ///// <summary>
    ///// Completes when the game thread is initialized and ready.
    ///// </summary>
    //public Task Started => startedSource.Task;

    ///// <summary>
    ///// Completes when the game loop has stopped.
    ///// </summary>
    //public Task Completion => stoppedSource.Task;

    ///// <summary>
    ///// Registers a system that is updated on every iteration of the loop.
    ///// </summary>
    ///// <remarks>
    ///// Systems must be added before <see cref="Start"/>. They are updated in
    ///// the order they were added, on the game thread, after due timers have
    ///// fired and before posted callbacks run.
    ///// </remarks>
    //public void AddSystem(ISimulationSystem system)
    //{
    //    ArgumentNullException.ThrowIfNull(system);

    //    lock (lifecycleLock)
    //    {
    //        ThrowIfDisposed();

    //        if (state != GameLoopState.Created)
    //            throw new InvalidOperationException("Systems can only be added before the game loop is started.");

    //        systems.Add(system);
    //    }
    //}

    ///// <summary>
    ///// Starts the game loop on a dedicated background thread.
    ///// </summary>
    //public void Start()
    //{
    //    lock (lifecycleLock)
    //    {
    //        ThrowIfDisposed();

    //        if (state != GameLoopState.Created)
    //        {
    //            throw new InvalidOperationException(
    //                "The game loop can only be started once.");
    //        }

    //        state = GameLoopState.Starting;

    //        gameThread = new Thread(Run)
    //        {
    //            IsBackground = true,
    //            Name = "NullByte Game Thread"
    //        };

    //        gameThread.Start();
    //    }
    //}

    ///// <summary>
    ///// Requests that the game loop stop.
    ///// </summary>
    //public void RequestStop()
    //{
    //    lock (lifecycleLock)
    //    {
    //        if (state is
    //            GameLoopState.Stopping or
    //            GameLoopState.Stopped or
    //            GameLoopState.Faulted or
    //            GameLoopState.Disposed)
    //        {
    //            return;
    //        }

    //        if (state == GameLoopState.Created)
    //        {
    //            state = GameLoopState.Stopped;

    //            startedSource.TrySetCanceled();
    //            stoppedSource.TrySetResult();

    //            return;
    //        }

    //        state = GameLoopState.Stopping;
    //    }

    //    stopSource.Cancel();
    //}

    ///// <summary>
    ///// Posts work for asynchronous execution on the game thread.
    ///// </summary>
    //public void Post(SendOrPostCallback callback, object? state = null)
    //{
    //    ArgumentNullException.ThrowIfNull(callback);

    //    lock (lifecycleLock)
    //    {
    //        ThrowIfDisposed();

    //        if (this.state != GameLoopState.Running)
    //            throw new InvalidOperationException("Work can only be posted while the game loop is running.");

    //        synchronizationContext.Post(callback, state);
    //    }
    //}

    ///// <summary>
    ///// Executes an asynchronous operation on the game thread and
    ///// completes when the operation has finished.
    ///// </summary>
    ///// <param name="callback">The asynchronous operation to execute on the game thread.</param>
    ///// <returns>A task that represents the completion of the operation.</returns>
    //public Task InvokeAsync(Func<Task> callback)
    //{
    //    ArgumentNullException.ThrowIfNull(callback);

    //    var completionSource = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously);
    //    Post(_ => { _ = ExecuteAsync(callback, completionSource); });

    //    return completionSource.Task;
    //}

    ///// <summary>
    ///// Executes an asynchronous operation on the game thread and
    ///// returns its result when the operation has finished.
    ///// </summary>
    ///// <typeparam name="T">The type of result produced by the operation.</typeparam>
    ///// <param name="callback">The asynchronous operation to execute on the game thread.</param>
    ///// <returns>A task that contains the result produced by the operation.</returns>
    //public Task<T> InvokeAsync<T>(Func<Task<T>> callback)
    //{
    //    ArgumentNullException.ThrowIfNull(callback);

    //    var completionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    //    Post(_ => { _ = ExecuteAsync(callback, completionSource); });

    //    return completionSource.Task;
    //}

    //private static async Task ExecuteAsync(Func<Task> callback, TaskCompletionSource completionSource)
    //{
    //    try
    //    {
    //        await callback();
    //        completionSource.TrySetResult();
    //    }
    //    catch (OperationCanceledException exception)
    //    {
    //        completionSource.TrySetCanceled(exception.CancellationToken);
    //    }
    //    catch (Exception exception)
    //    {
    //        completionSource.TrySetException(exception);
    //    }
    //}


    //private static async Task ExecuteAsync<T>(Func<Task<T>> callback, TaskCompletionSource<T> completionSource)
    //{
    //    try
    //    {
    //        var result = await callback();
    //        completionSource.TrySetResult(result);
    //    }
    //    catch (OperationCanceledException exception)
    //    {
    //        completionSource.TrySetCanceled(exception.CancellationToken);
    //    }
    //    catch (Exception exception)
    //    {
    //        completionSource.TrySetException(exception);
    //    }
    //}

    ///// <summary>
    ///// Waits until the game loop has stopped.
    ///// </summary>
    //public Task WaitForStopAsync(CancellationToken cancellationToken = default)
    //    => Completion.WaitAsync(cancellationToken);

    //private void Run()
    //{
    //    Exception? failure = null;

    //    try
    //    {
    //        synchronizationContext.BindToCurrentThread();

    //        clock.Start();
    //        lastSystemUpdate = clock.Elapsed;

    //        lock (lifecycleLock)
    //        {
    //            if (state != GameLoopState.Stopping)
    //                state = GameLoopState.Running;

    //            startedSource.TrySetResult();
    //        }

    //        while (!stopSource.IsCancellationRequested)
    //        {
    //            RunIteration();

    //            Thread.Sleep(DefaultIdleDelayMilliseconds);
    //        }

    //        // This runs the final iteration
    //        RunIteration();
    //    }
    //    catch (Exception exception)
    //    {
    //        failure = exception;
    //    }
    //    finally
    //    {
    //        synchronizationContext.Complete();

    //        clock.Stop();

    //        SynchronizationContext.SetSynchronizationContext(null);
    //    }

    //    if (failure is null)
    //    {
    //        lock (lifecycleLock)
    //        {
    //            state = GameLoopState.Stopped;
    //        }

    //        stoppedSource.TrySetResult();
    //    }
    //    else
    //    {
    //        lock (lifecycleLock)
    //        {
    //            state = GameLoopState.Faulted;
    //        }

    //        startedSource.TrySetException(failure);
    //        stoppedSource.TrySetException(failure);
    //    }
    //}

    //private void RunIteration()
    //{
    //    scheduler.Update();
    //    UpdateSystems();
    //    DrainPendingWork();
    //}

    ///*
    // * Every system sees the same delta, measured on the game clock, so game
    // * speed changes and a paused clock apply to all of them alike. As with
    // * posted work, a failing system is reported when someone listens and is
    // * fatal otherwise; it never stops the systems after it.
    // */
    //private void UpdateSystems()
    //{
    //    var now = clock.Elapsed;
    //    var delta = now - lastSystemUpdate;
    //    lastSystemUpdate = now;

    //    if (delta <= TimeSpan.Zero)
    //        return;

    //    foreach (var system in systems)
    //    {
    //        try
    //        {
    //            system.Update(delta);
    //        }
    //        catch (Exception exception) when (UnhandledException is not null)
    //        {
    //            ReportUnhandled(exception);
    //        }
    //    }
    //}

    ///*
    // * One batch per tick: continuations released by Scheduler.Update run in
    // * this batch, while anything they post waits for the next tick.
    // */
    //private void DrainPendingWork()
    //{
    //    try
    //    {
    //        synchronizationContext.DrainBatch();
    //    }
    //    catch (AggregateException aggregate) when (UnhandledException is not null)
    //    {
    //        foreach (var exception in aggregate.InnerExceptions)
    //            ReportUnhandled(exception);
    //    }
    //}

    //private void ReportUnhandled(Exception exception)
    //{
    //    try
    //    {
    //        UnhandledException?.Invoke(exception);
    //    }
    //    catch
    //    {
    //        // A failing handler must not take down the game loop.
    //    }
    //}

    //private void ThrowIfDisposed()
    //{
    //    ObjectDisposedException.ThrowIf(state == GameLoopState.Disposed, this);
    //}

    //public async ValueTask DisposeAsync()
    //{
    //    bool shouldWait;

    //    lock (lifecycleLock)
    //    {
    //        if (state == GameLoopState.Disposed)
    //            return;

    //        shouldWait =
    //            state is not
    //                GameLoopState.Created and not
    //                GameLoopState.Stopped and not
    //                GameLoopState.Faulted;

    //        if (state == GameLoopState.Created)
    //        {
    //            startedSource.TrySetCanceled();
    //            stoppedSource.TrySetResult();
    //        }
    //        else if (shouldWait &&
    //                 state != GameLoopState.Stopping)
    //        {
    //            state = GameLoopState.Stopping;
    //        }
    //    }

    //    if (shouldWait)
    //    {
    //        stopSource.Cancel();

    //        try
    //        {
    //            await Completion.ConfigureAwait(false);
    //        }
    //        catch
    //        {
    //            // Disposal must still release owned resources.
    //        }
    //    }

    //    lock (lifecycleLock)
    //    {
    //        state = GameLoopState.Disposed;
    //    }

    //    stopSource.Dispose();
    //}

    //private enum GameLoopState
    //{
    //    Created,
    //    Starting,
    //    Running,
    //    Stopping,
    //    Stopped,
    //    Faulted,
    //    Disposed
    //}
}