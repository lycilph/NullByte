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

    private readonly ServerId processHostServerId;
    private readonly int processThreads;
    private readonly CancellationToken cancellationToken;

    public NullByteApi(
        GameState gameState,
        GameScheduler scheduler,
        NetworkService networkService,
        BotnetService botnetService,
        CloudServerService cloudServerService,
        ServerActions serverActions,
        ActivityService activityService,
        SkillFormulas skillFormulas,
        BotnetFormulas botnetFormulas,
        IScriptOperations scriptOperations,
        IGameOutput output,
        ServerId processHostServerId,
        int processThreads = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(botnetService);
        ArgumentNullException.ThrowIfNull(cloudServerService);
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
        this.processHostServerId = processHostServerId;
        this.processThreads = processThreads;

        Files = new NullByteFilesApi(gameState.FileSystem, gameState.Network, processHostServerId, gameState.Narrative);
        Botnet = new NullByteBotnetApi(gameState.Botnet, botnetService, botnetFormulas, gameState.Narrative);
        Cloud = new NullByteCloudApi(cloudServerService, gameState.Narrative);
        Activities = new NullByteActivitiesApi(gameState, activityService, skillFormulas, cancellationToken);
        Player = new NullBytePlayerApi(gameState.Player, skillFormulas, gameState.Narrative);
    }

    public INullByteFilesApi Files { get; }
    public INullByteBotnetApi Botnet { get; }
    public INullByteCloudApi Cloud { get; }
    public INullByteActivitiesApi Activities { get; }
    public INullBytePlayerApi Player { get; }

    public void Print(object? value) { Require("scripts.execute"); output.WriteLine(value?.ToString() ?? string.Empty); }
    public Task Sleep(TimeSpan duration) { Require("scripts.execute"); return scheduler.DelayAsync(duration, cancellationToken); }
    public Task<HackResult> Hack(string hostname = "") { Require("server.operations"); return serverActions.HackAsync(GetTargetHostname(hostname), processThreads, cancellationToken); }
    public Task<GrowResult> Grow(string hostname = "") { Require("server.operations"); return serverActions.GrowAsync(GetTargetHostname(hostname), processThreads, cancellationToken); }
    public Task<WeakenResult> Weaken(string hostname = "") { Require("server.operations"); return serverActions.WeakenAsync(GetTargetHostname(hostname), processThreads, cancellationToken); }
    public NukeResult Nuke(string hostname = "") { Require("server.root-access"); return serverActions.Nuke(GetTargetHostname(hostname)); }
    public IReadOnlyList<string> Scan(string hostname = "") { Require("network.scan"); return networkService.Scan(GetTargetHostname(hostname)).Select(s => s.Hostname).ToList(); }
    public double GetServerMoneyAvailable(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).Money; }
    public double GetServerMaxMoney(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).MaximumMoney; }
    public double GetServerSecurityLevel(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).Security; }
    public double GetServerMinSecurityLevel(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).MinimumSecurity; }
    public int GetServerRequiredHackingLevel(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).RequiredHackingLevel; }
    public double GetServerMaxRam(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).MaximumRam; }
    public double GetServerUsedRam(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).UsedRam; }
    public bool HasRootAccess(string hostname = "") { Require("network.inspect"); return GetServer(GetTargetHostname(hostname)).HasRootAccess; }
    public IReadOnlyList<ScriptProcessInfo> Ps(string hostname = "") { Require("scripts.inspect"); return scriptOperations.ProcessesRunning(GetTargetHostname(hostname)); }
    public async Task<int> RunAsync(string filename, params string[] args) { Require("scripts.execute"); return await scriptOperations.StartSimple(gameState.Network.GetServer(processHostServerId).Hostname, filename, args, processThreads); }
    public async Task<int> ExecAsync(string filename, string hostname, params string[] args) { Require("scripts.execute"); return await scriptOperations.StartSimple(hostname, filename, args, processThreads); }
    public bool Kill(int pid) { Require("scripts.control"); return scriptOperations.Kill(pid); }
    public int KillAll() { Require("scripts.control"); return scriptOperations.KillAll(); }
    public bool ScriptRunning(string filename, string hostname = "") { Require("scripts.inspect"); return scriptOperations.ScriptRunning(filename, GetTargetHostname(hostname)); }
    public double GetScriptRam(string filename, string hostname = "") { Require("scripts.inspect"); return scriptOperations.GetScriptRam(filename, GetTargetHostname(hostname)); }

    private void Require(string capability) => gameState.Narrative.EnsureCapability(capability);

    private string GetTargetHostname(string hostname) => string.IsNullOrWhiteSpace(hostname) ? gameState.Network.GetServer(processHostServerId).Hostname : hostname;

    private Server GetServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        if (!gameState.Network.TryGetServer(hostname, out var server))
            throw new InvalidOperationException($"Server '{hostname}' does not exist.");
        return server!;
    }
}