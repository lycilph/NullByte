using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
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
    public required IGameOutput Output { get; init; }       // the host provides this
    public BalanceConfig Balance { get; init; } = new();
    public GameState? State { get; init; }                  // null = new game; M9 passes a loaded one
    public IGameClock? Clock { get; init; }                 // tests pass FakeGameClock
    public IRandomSource? Random { get; init; }             // tests pass a fixed source
    public INullByteApi? Api { get; init; }                 // tests pass a fake api
}

public sealed record GameServices(
    NetworkService Network,
    ServerActions Actions,
    BotnetService Botnet,
    ScriptService Scripts,
    HackingFormulas HackingFormulas,
    BotnetFormulas BotnetFormulas);

public sealed class GameSession : IAsyncDisposable
{
    private int disposed;
    private readonly GameLoop loop;
    private readonly IGameOutput output;
    private readonly IScriptCompiler compiler;

    // Read access for hosts and commands
    public GameState State { get; }
    public IGameClock Clock { get; }
    public IGameThread GameThread { get; }
    public GameServices Services { get; }

    internal GameSession(GameState state, IGameClock clock, GameLoop loop, GameServices services, IGameOutput output, IScriptCompiler compiler)
    {
        this.loop = loop;
        this.output = output;
        this.compiler = compiler;

        State = state;
        Clock = clock;
        GameThread = loop;
        Services = services;
    }

    public async Task StartAsync()
    {
        try
        {
            // Warm up the script compiler (should i await this somewhere???)
            #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() => compiler.Compile("{}", "warmup"));
            #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            loop.Start();              // throws if already started
            await loop.Started;        // faults if the game thread failed to initialise
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
            // M6: Services.Scripts.KillAll();   // cancel tokens so scripts unwind and unload their load contexts
            loop.RequestStop();                   // wakes the loop; one final tick runs queued work
            await loop.Completion;                // game thread has exited
        }
        catch (Exception exception)
        {
            output.WriteLine($"Game loop stopped with an error: {exception.Message}");
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
        var clock = options.Clock ?? new RealGameClock() { SpeedMultiplier = options.Balance.GameSpeedMultiplier };

        var synchronizationContext = new GameSynchronizationContext();
        var scheduler = new GameScheduler(clock);
        var gameLoop = new GameLoop(clock, synchronizationContext, scheduler);

        var state = options.State ?? InitialGameStateFactory.Create();
        var randomSource = options.Random ?? new SeededRandomSource(Random.Shared.Next());

        var hackingFormulas = new HackingFormulas(options.Balance);
        var botnetFormulas = new BotnetFormulas(options.Balance);

        var networkService = new NetworkService(state);
        var serverActions = new ServerActions(state, scheduler, hackingFormulas, randomSource);
        var botnetService = new BotnetService(state, botnetFormulas);

        var scriptCompiler = new ScriptCompiler();
        var scriptLoader = new ScriptAssemblyLoader();
        var scriptRunner = new ScriptRunner(scriptLoader);
        var api = options.Api ?? new NullByteApi(state, scheduler, serverActions, options.Output);
        var scriptService = new ScriptService(state, scriptCompiler, scriptRunner, api);

        var services = new GameServices(networkService, serverActions, botnetService, scriptService, hackingFormulas, botnetFormulas);

        // Systems are updated by the game loop on the game thread; they must be added before it starts.
        gameLoop.AddSystem(new BotnetSystem(state, botnetFormulas));

        scriptService.ScriptFailed += exception => options.Output.WriteLine($"Script failed: {exception.Message}");
        gameLoop.UnhandledException += exception => options.Output.WriteLine($"Unhandled error on the game thread: {exception.Message}");

        return new GameSession(state, clock, gameLoop, services, options.Output, scriptCompiler);
    }
}