using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Persistence;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Simulation;
using NullByte.Core.Systems;
using NullByte.Core.Threading;
using NullByte.Core.Time;
using NullByte.Core.World;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Shell;

public sealed record GameSessionOptions
{
    public required IGameOutput Output { get; init; }                           // the host provides this
    public BalanceConfig Balance { get; init; } = new();
    public TimeSpan InitialGameTime { get; init; } = TimeSpan.Zero;             // Used when loading a saved game
    public IReadOnlyList<ProcessSave> ProcessesToRestart { get; init; } = [];   // Used when loading a saved game
    public GameState? State { get; init; }                                      // null = new game; a loaded game passes its restored state
    public IGameClock? Clock { get; init; }                                     // tests pass FakeGameClock
    public IRandomSource? Random { get; init; }                                 // tests pass a fixed source
    public INullByteApiFactory? ApiFactory { get; init; }                       // tests pass a fake api factory
    public ActivityCatalog? ActivityCatalog { get; init; }                      // tests pass a small catalog; null = the one that ships with the game
    public IScriptCompiler? ScriptCompiler { get; init; }                       // tests can provide a compiler; null = create the default compiler

    public GameSessionOptions WithSave(RestoredGame restored)
    {
        ArgumentNullException.ThrowIfNull(restored);

        return this with
        {
            State = restored.State,
            InitialGameTime = restored.GameTime,
            ProcessesToRestart = restored.Processes,

            // The restored activity was built from this catalog, so the session should use the same one.
            ActivityCatalog = restored.ActivityCatalog ?? ActivityCatalog
        };
    }
}

public sealed record GameServices(
    NetworkService Network,
    ServerActions Actions,
    BotnetService Botnet,
    ScriptService Scripts,
    ActivityService Activities,
    FormulaSet Formulas);

public sealed class GameSession : IAsyncDisposable
{
    private int disposed;
    private readonly GameLoop loop;
    private readonly IGameOutput output;
    private readonly IScriptCompiler compiler;
    private readonly IReadOnlyList<ProcessSave> processesToRestart;

    // Read access for hosts and commands
    public GameState State { get; }
    public IGameClock Clock { get; }
    public IGameThread GameThread { get; }
    public GameServices Services { get; }

    internal GameSession(
        GameState state,
        IGameClock clock,
        GameLoop loop,
        GameServices services,
        IGameOutput output,
        IScriptCompiler compiler,
        IReadOnlyList<ProcessSave>? processesToRestart = null)
    {
        this.loop = loop;
        this.output = output;
        this.compiler = compiler;
        this.processesToRestart = processesToRestart ?? [];

        State = state;
        Clock = clock;
        GameThread = loop;
        Services = services;
    }

    public async Task StartAsync()
    {
        try
        {
            loop.Start();              // throws if already started
            await loop.Started;        // faults if the game thread failed to initialise
            await RestartSavedProcessesAsync();
        }
        catch (Exception exception)
        {
            output.WriteLine($"Game loop failed to start with error: {exception.Message}");
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await KillScriptsAsync();             // cancel tokens so scripts unwind and unload their load contexts
            loop.RequestStop();                   // wakes the loop; one final tick runs queued work
            await loop.Completion;                // game thread has exited
        }
        catch (Exception exception)
        {
            output.WriteLine($"Game loop stopped with an error: {exception.Message}");
        }
    }

    public Task<SaveGame> CaptureSaveAsync(DateTimeOffset savedAtUtc)
        => loop.InvokeAsync(() => Task.FromResult(
            SaveGameMapper.Capture(State, Clock.Elapsed, Services.Scripts.CaptureRunningProcesses(), savedAtUtc)));

    private async Task RestartSavedProcessesAsync()
    {
        if (processesToRestart.Count == 0)
            return;

        output.WriteLine($"Restarting {processesToRestart.Count} script(s)...");

        var results = await loop.InvokeAsync(() => Services.Scripts.RestoreProcessesAsync(processesToRestart));

        foreach (var result in results.Where(result => !result.Start.Succeeded))
        {
            output.WriteLine(
                $"  Could not restart {result.Saved.Filename} " +
                $"on {State.Network.GetServer(ServerId.Parse(result.Saved.ServerId)).Hostname}: {result.Start.FailureMessage}");
        }
    }

    private async Task KillScriptsAsync()
    {
        if (!loop.IsRunning)
            return;

        try
        {
            await loop.InvokeAsync(() => { Services.Scripts.KillAll(); return Task.CompletedTask; });
        }
        catch (InvalidOperationException)
        {
            // The loop stopped between the check and the call: nothing left to kill.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        try { await StopAsync(); }
        catch { /* disposal must not throw; StopAsync is where you observe failures */ }

        await loop.DisposeAsync();     // disposes the CancellationTokenSource etc.
                                       // dispose anything else the session owns (clock if it's disposable)
    }

    public static GameSession Create(GameSessionOptions options)
    {
        var clock = options.Clock 
            ?? new RealGameClock(options.InitialGameTime) { SpeedMultiplier = options.Balance.GameSpeedMultiplier };

        var synchronizationContext = new GameSynchronizationContext();
        var scheduler = new GameScheduler(clock, synchronizationContext.VerifyAccess);
        var gameLoop = new GameLoop(clock, synchronizationContext, scheduler);

        var state = options.State ?? InitialGameStateFactory.Create();
        var randomSource = options.Random ?? new SeededRandomSource(Random.Shared.Next());

        var formulas = new FormulaSet(options.Balance);

        var networkService = new NetworkService(state, scheduler, formulas.Network);
        var serverActions = new ServerActions(state, scheduler, formulas.ServerOperations, formulas.Skills, randomSource);
        var botnetService = new BotnetService(state, formulas.Botnet);

        var activityCatalog = options.ActivityCatalog ?? ActivityCatalogFactory.CreateDefault();
        var activityService = new ActivityService(state, activityCatalog, formulas.Activities);

        var scriptCompiler = options.ScriptCompiler ?? new ScriptCompiler();
        var scriptLoader = new ScriptAssemblyLoader();
        var scriptRunner = new ScriptRunner(scriptLoader);
        var scriptProcessManager = new ScriptProcessManager();
        var apiFactory = options.ApiFactory 
            ?? new NullByteApiFactory(
                state,
                scheduler,
                networkService,
                botnetService,
                serverActions,
                activityService,
                formulas,
                options.Output);
        var scriptService = new ScriptService(state, scriptCompiler, scriptRunner, apiFactory, scriptProcessManager);

        var services = new GameServices(networkService, serverActions, botnetService, scriptService, activityService, formulas);

        // Systems are updated by the game loop on the game thread; they must be added before it starts.
        gameLoop.AddSystem(new BotnetSystem(state, formulas.Botnet));
        gameLoop.AddSystem(new ActivitySystem(state, formulas.Activities, activityService, randomSource));

        scriptService.ProcessFailed += (proc, exception) => options.Output.WriteLine($"Process {proc.Pid} failed: {exception.Message}");
        gameLoop.UnhandledException += exception => options.Output.WriteLine($"Unhandled error on the game thread: {exception.Message}");

        return new GameSession(state, clock, gameLoop, services, options.Output, scriptCompiler, options.ProcessesToRestart);
    }
}