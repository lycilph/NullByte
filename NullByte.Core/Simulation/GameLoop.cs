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

    private Thread? gameThread;
    private bool hasStarted;
    private bool hasDisposed;

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
    /// Gets the game clock used by this game loop.
    /// </summary>
    public IGameClock Clock => clock;

    /// <summary>
    /// Gets whether the game loop has been started.
    /// </summary>
    public bool HasStarted
    {
        get
        {
            lock (lifecycleLock)
                return hasStarted;
        }
    }

    /// <summary>
    /// Gets whether the calling thread is the game thread.
    /// </summary>
    public bool IsGameThread => synchronizationContext.IsGameThread;

    /// <summary>
    /// Gets a task that completes when the game thread is ready.
    /// </summary>
    public Task Started => startedSource.Task;

    /// <summary>
    /// Gets a task that completes when the game loop has stopped.
    /// </summary>
    public Task Completion => stoppedSource.Task;

    /// <summary>
    /// Starts the game loop on a dedicated background thread.
    /// </summary>
    public void Start()
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(hasDisposed, this);

            if (hasStarted)
                throw new InvalidOperationException( "The game loop can only be started once.");

            hasStarted = true;

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
        if (!stopSource.IsCancellationRequested)
            stopSource.Cancel();
    }

    /// <summary>
    /// Posts work for asynchronous execution on the game thread.
    /// </summary>
    public void Post(SendOrPostCallback callback, object? state = null)
    {
        ArgumentNullException.ThrowIfNull(callback);

        ThrowIfNotStarted();

        if (stopSource.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Work cannot be posted after the game loop " +
                "has been asked to stop.");
        }

        synchronizationContext.Post(callback, state);
    }

    /// <summary>
    /// Waits until the game loop has stopped.
    /// </summary>
    public Task WaitForStopAsync(CancellationToken cancellationToken = default)
        => Completion.WaitAsync(cancellationToken);

    private void Run()
    {
        try
        {
            synchronizationContext.BindToCurrentThread();

            clock.Start();

            startedSource.TrySetResult();

            while (!stopSource.IsCancellationRequested)
            {
                RunIteration();

                Thread.Sleep(DefaultIdleDelayMilliseconds);
            }

            /*
             * Process work that was already queued before shutdown.
             * New work is rejected once RequestStop has been called.
             */
            RunFinalIteration();

            stoppedSource.TrySetResult();
        }
        catch (Exception exception)
        {
            startedSource.TrySetException(exception);
            stoppedSource.TrySetException(exception);
        }
        finally
        {
            clock.Stop();

            /*
             * Avoid leaving this context installed if the thread is
             * reused by hosting infrastructure in a future version.
             */
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
        }
    }

    private void RunIteration()
    {
        /*
         * Ordering matters:
         *
         * 1. Complete game-time delays that are now due.
         * 2. Execute their continuations and other posted work.
         */
        Scheduler.Update();
        synchronizationContext.Drain();
    }

    private void RunFinalIteration()
    {
        Scheduler.Update();
        synchronizationContext.Drain();
    }

    private void ThrowIfNotStarted()
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(
                hasDisposed,
                this);

            if (!hasStarted)
            {
                throw new InvalidOperationException(
                    "The game loop has not been started.");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (lifecycleLock)
        {
            if (hasDisposed)
                return;

            hasDisposed = true;
        }

        RequestStop();

        if (HasStarted)
        {
            try
            {
                await Completion.ConfigureAwait(false);
            }
            catch
            {
                /*
                 * Completion retains the original exception for callers.
                 * Disposal still needs to release owned resources.
                 */
            }
        }

        stopSource.Dispose();
    }
}