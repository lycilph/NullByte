using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.Core.Scripting;

/// <summary>
/// Provides the API exposed to player-written scripts through the global
/// <c>nb</c> object.
/// </summary>
public sealed class NullByteApi : INullByteApi
{
    private readonly GameState gameState;
    private readonly GameScheduler scheduler;
    private readonly NetworkService networkService;
    private readonly ServerActions serverActions;
    private readonly HackingFormulas hackingFormulas;
    private readonly IScriptOperations scriptOperations;
    private readonly IGameOutput output;

    private readonly int threads;
    private readonly CancellationToken cancellationToken;

    public NullByteApi(
        GameState gameState,
        GameScheduler scheduler,
        NetworkService networkService,
        ServerActions serverActions,
        HackingFormulas hackingFormulas,
        IScriptOperations scriptOperations,
        IGameOutput output,
        int threads = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(serverActions);
        ArgumentNullException.ThrowIfNull(hackingFormulas);
        ArgumentNullException.ThrowIfNull(scriptOperations);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        this.gameState = gameState;
        this.scheduler = scheduler;
        this.networkService = networkService;
        this.serverActions = serverActions;
        this.output = output;
        this.hackingFormulas = hackingFormulas;
        this.scriptOperations = scriptOperations;
        this.cancellationToken = cancellationToken;
        this.threads = threads;
    }

    public void Print(object? value) => output.WriteLine(value?.ToString() ?? string.Empty);

    public Task Sleep(TimeSpan duration) => scheduler.DelayAsync(duration, cancellationToken);

    public Task<HackResult> Hack(string hostname) 
        => serverActions.HackAsync(hostname, threads, cancellationToken);

    public Task<GrowResult> Grow(string hostname)
        => serverActions.GrowAsync(hostname, threads, cancellationToken);

    public Task<WeakenResult> Weaken(string hostname)
        => serverActions.WeakenAsync(hostname, threads, cancellationToken);

    public NukeResult Nuke(string hostname) => serverActions.Nuke(hostname);

    public IReadOnlyList<string> Scan(string hostname = "")
        => networkService.Scan(hostname).Select(s => s.Hostname).ToList();

    public double GetHackingLevel()
        => hackingFormulas.CalculateHackingLevel(gameState.Player.HackingExperience);

    public double GetServerMoneyAvailable(string hostname) => GetServer(hostname).Money;
    public double GetServerMaxMoney(string hostname) => GetServer(hostname).MaximumMoney;
    public double GetServerSecurityLevel(string hostname) => GetServer(hostname).Security;
    public double GetServerMinSecurityLevel(string hostname) => GetServer(hostname).MinimumSecurity;

    public double GetServerRequiredHackingLevel(string hostname) => GetServer(hostname).RequiredHackingLevel;
    public double GetServerMaxRam(string hostname) => GetServer(hostname).MaximumRam;
    public double GetServerUsedRam(string hostname) => GetServer(hostname).UsedRam;
    public bool HasRootAccess(string hostname) => GetServer(hostname).HasRootAccess;

    public IReadOnlyList<ScriptProcessInfo> Ps(string hostname = "") => scriptOperations.Ps(hostname);
    public bool Kill(int pid) => scriptOperations.Kill(pid);
    public bool ScriptRunning(string filename, string hostname = "") => scriptOperations.ScriptRunning(filename, hostname);
    public double GetScriptRam(string filename, string hostname = "") => scriptOperations.GetScriptRam(filename, hostname);

    private Server GetServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        if (!gameState.Network.TryGetServer(hostname, out var server))
            throw new InvalidOperationException($"Server '{hostname}' does not exist.");

        return server!;
    }
}