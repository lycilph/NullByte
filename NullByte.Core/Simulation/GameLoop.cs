using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Simulation;

/// <summary>
/// Runs the core game simulation on a dedicated thread.
/// </summary>
public sealed class GameLoop : IAsyncDisposable
{
    private const int DefaultIdleDelayMilliseconds = 1;

    private readonly RealGameClock clock;
    private readonly GameSynchronizationContext synchronizationContext;
    private readonly CancellationTokenSource stopSource = new();

    private readonly TaskCompletionSource startedSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource stoppedSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Lock lifecycleLock = new();

    private GameLoopState state = GameLoopState.Created;
    private Thread? gameThread;

    public GameLoop(
        RealGameClock clock,
        GameSynchronizationContext synchronizationContext)
    {
        this.clock = clock
            ?? throw new ArgumentNullException(nameof(clock));

        this.synchronizationContext = synchronizationContext
            ?? throw new ArgumentNullException(
                nameof(synchronizationContext));

        Scheduler = new GameScheduler(clock);
    }

    /// <summary>
    /// Gets the scheduler owned by this game loop.
    /// </summary>
    public GameScheduler Scheduler { get; }

    /// <summary>
    /// Gets the synchronization context used by the game thread.
    /// </summary>
    public GameSynchronizationContext SynchronizationContext => synchronizationContext;

    /// <summary>
    /// Gets the game clock.
    /// </summary>
    public IGameClock Clock => clock;

    /// <summary>
    /// Gets whether the game loop is currently running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (lifecycleLock)
            {
                return state == GameLoopState.Running;
            }
        }
    }

    /// <summary>
    /// Gets whether the calling thread is the game thread.
    /// </summary>
    public bool IsGameThread =>
        synchronizationContext.IsGameThread;

    /// <summary>
    /// Completes when the game thread is initialized and ready.
    /// </summary>
    public Task Started => startedSource.Task;

    /// <summary>
    /// Completes when the game loop has stopped.
    /// </summary>
    public Task Completion => stoppedSource.Task;

    /// <summary>
    /// Starts the game loop on a dedicated background thread.
    /// </summary>
    public void Start()
    {
        lock (lifecycleLock)
        {
            ThrowIfDisposed();

            if (state != GameLoopState.Created)
            {
                throw new InvalidOperationException(
                    "The game loop can only be started once.");
            }

            state = GameLoopState.Starting;

            gameThread = new Thread(Run)
            {
                IsBackground = true,
                Name = "NullByte Game Thread"
            };

            gameThread.Start();
        }
    }

    /// <summary>
    /// Requests that the game loop stop.
    /// </summary>
    public void RequestStop()
    {
        lock (lifecycleLock)
        {
            if (state is
                GameLoopState.Stopping or
                GameLoopState.Stopped or
                GameLoopState.Faulted or
                GameLoopState.Disposed)
            {
                return;
            }

            if (state == GameLoopState.Created)
            {
                state = GameLoopState.Stopped;

                startedSource.TrySetCanceled();
                stoppedSource.TrySetResult();

                return;
            }

            state = GameLoopState.Stopping;
        }

        stopSource.Cancel();
    }

    /// <summary>
    /// Posts work for asynchronous execution on the game thread.
    /// </summary>
    public void Post(SendOrPostCallback callback, object? state = null)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (lifecycleLock)
        {
            ThrowIfDisposed();

            if (this.state != GameLoopState.Running)
            {
                throw new InvalidOperationException(
                    "Work can only be posted while the " +
                    "game loop is running.");
            }

            synchronizationContext.Post(callback, state);
        }
    }

    /// <summary>
    /// Waits until the game loop has stopped.
    /// </summary>
    public Task WaitForStopAsync(CancellationToken cancellationToken = default)
        => Completion.WaitAsync(cancellationToken);

    private void Run()
    {
        Exception? failure = null;

        try
        {
            synchronizationContext.BindToCurrentThread();

            clock.Start();

            lock (lifecycleLock)
            {
                if (state != GameLoopState.Stopping)
                    state = GameLoopState.Running;

                startedSource.TrySetResult();
            }

            while (!stopSource.IsCancellationRequested)
            {
                RunIteration();

                Thread.Sleep(
                    DefaultIdleDelayMilliseconds);
            }

            RunFinalIteration();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            synchronizationContext.Complete();

            clock.Stop();

            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
        }

        if (failure is null)
        {
            lock (lifecycleLock)
            {
                state = GameLoopState.Stopped;
            }

            stoppedSource.TrySetResult();
        }
        else
        {
            lock (lifecycleLock)
            {
                state = GameLoopState.Faulted;
            }

            startedSource.TrySetException(failure);
            stoppedSource.TrySetException(failure);
        }
    }

    private void RunIteration()
    {
        Scheduler.Update();
        synchronizationContext.Drain();
    }

    private void RunFinalIteration()
    {
        Scheduler.Update();
        synchronizationContext.Drain();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(state == GameLoopState.Disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        bool shouldWait;

        lock (lifecycleLock)
        {
            if (state == GameLoopState.Disposed)
                return;

            shouldWait =
                state is not
                    GameLoopState.Created and not
                    GameLoopState.Stopped and not
                    GameLoopState.Faulted;

            if (state == GameLoopState.Created)
            {
                startedSource.TrySetCanceled();
                stoppedSource.TrySetResult();
            }
            else if (shouldWait &&
                     state != GameLoopState.Stopping)
            {
                state = GameLoopState.Stopping;
            }
        }

        if (shouldWait)
        {
            stopSource.Cancel();

            try
            {
                await Completion.ConfigureAwait(false);
            }
            catch
            {
                // Disposal must still release owned resources.
            }
        }

        lock (lifecycleLock)
        {
            state = GameLoopState.Disposed;
        }

        stopSource.Dispose();
    }

    private enum GameLoopState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        Faulted,
        Disposed
    }
}