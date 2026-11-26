using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Scripting.Activities;
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
    private readonly SkillFormulas skillFormulas;
    private readonly IScriptOperations scriptOperations;
    private readonly IGameOutput output;

    private readonly string processHostname;
    private readonly int processThreads;
    private readonly CancellationToken cancellationToken;

    public NullByteApi(
        GameState gameState,
        GameScheduler scheduler,
        NetworkService networkService,
        BotnetService botnetService,
        ServerActions serverActions,
        ActivityService activityService,
        SkillFormulas skillFormulas,
        BotnetFormulas botnetFormulas,
        IScriptOperations scriptOperations,
        IGameOutput output,
        string processHostname,
        int processThreads = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(botnetService);
        ArgumentNullException.ThrowIfNull(serverActions);
        ArgumentNullException.ThrowIfNull(activityService);
        ArgumentNullException.ThrowIfNull(botnetFormulas);
        ArgumentNullException.ThrowIfNull(skillFormulas);
        ArgumentNullException.ThrowIfNull(scriptOperations);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfLessThan(processThreads, 1);

        this.gameState = gameState;
        this.scheduler = scheduler;
        this.networkService = networkService;
        this.serverActions = serverActions;
        this.output = output;
        this.skillFormulas = skillFormulas;
        this.scriptOperations = scriptOperations;
        this.cancellationToken = cancellationToken;
        this.processHostname = processHostname;
        this.processThreads = processThreads;

        Files = new NullByteFilesApi(gameState.FileSystem, processHostname);
        Botnet = new NullByteBotnetApi(gameState.Botnet, botnetService, botnetFormulas);
        Activities = new NullByteActivitiesApi(gameState, activityService, skillFormulas, cancellationToken);
    }

    public INullByteFilesApi Files { get; }
    public INullByteBotnetApi Botnet { get; }
    public INullByteActivitiesApi Activities { get; }

    public void Print(object? value) => output.WriteLine(value?.ToString() ?? string.Empty);

    public Task Sleep(TimeSpan duration) => scheduler.DelayAsync(duration, cancellationToken);

    public Task<HackResult> Hack(string hostname = "")
        => serverActions.HackAsync(GetTargetHostname(hostname), processThreads, cancellationToken);

    public Task<GrowResult> Grow(string hostname = "")
        => serverActions.GrowAsync(GetTargetHostname(hostname), processThreads, cancellationToken);

    public Task<WeakenResult> Weaken(string hostname = "")
        => serverActions.WeakenAsync(GetTargetHostname(hostname), processThreads, cancellationToken);

    public NukeResult Nuke(string hostname = "") => serverActions.Nuke(GetTargetHostname(hostname));

    public IReadOnlyList<string> Scan(string hostname = "")
        => networkService.Scan(GetTargetHostname(hostname)).Select(s => s.Hostname).ToList();

    public double GetHackingLevel()
        => skillFormulas.CalculateLevel(SkillKind.Hacking, gameState.Player.HackingExperience);

    public double GetServerMoneyAvailable(string hostname = "") => GetServer(GetTargetHostname(hostname)).Money;
    public double GetServerMaxMoney(string hostname = "") => GetServer(GetTargetHostname(hostname)).MaximumMoney;
    public double GetServerSecurityLevel(string hostname = "") => GetServer(GetTargetHostname(hostname)).Security;
    public double GetServerMinSecurityLevel(string hostname = "") => GetServer(GetTargetHostname(hostname)).MinimumSecurity;

    public int GetServerRequiredHackingLevel(string hostname = "") => GetServer(GetTargetHostname(hostname)).RequiredHackingLevel;
    public double GetServerMaxRam(string hostname = "") => GetServer(GetTargetHostname(hostname)).MaximumRam;
    public double GetServerUsedRam(string hostname = "") => GetServer(GetTargetHostname(hostname)).UsedRam;
    public bool HasRootAccess(string hostname = "") => GetServer(GetTargetHostname(hostname)).HasRootAccess;

    public IReadOnlyList<ScriptProcessInfo> Ps(string hostname = "") => scriptOperations.ProcessesRunning(GetTargetHostname(hostname));

    public async Task<int> RunAsync(string filename, params string[] args) => await scriptOperations.StartSimple(processHostname, filename, args, processThreads);
    public async Task<int> ExecAsync(string filename, string hostname, params string[] args) => await scriptOperations.StartSimple(hostname, filename, args, processThreads);

    public bool Kill(int pid) => scriptOperations.Kill(pid);
    public int KillAll() => scriptOperations.KillAll();
    public bool ScriptRunning(string filename, string hostname = "") => scriptOperations.ScriptRunning(filename, GetTargetHostname(hostname));
    public double GetScriptRam(string filename, string hostname = "") => scriptOperations.GetScriptRam(filename, GetTargetHostname(hostname));

    private string GetTargetHostname(string hostname) => string.IsNullOrWhiteSpace(hostname) ? processHostname : hostname;

    private Server GetServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        if (!gameState.Network.TryGetServer(hostname, out var server))
            throw new InvalidOperationException($"Server '{hostname}' does not exist.");

        return server!;
    }
}