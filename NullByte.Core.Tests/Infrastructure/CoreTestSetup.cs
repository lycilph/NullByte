using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Systems;
using NullByte.Core.Time;
using NullByte.Core.World;

namespace NullByte.Core.Tests.Infrastructure;

/// <summary>
/// What a Core test can ask for when it builds a <see cref="CoreTestWorld"/>. Everything has a default, so a test
/// only states what it cares about.
/// </summary>
internal sealed record CoreTestOptions
{
    /// <summary>The player's starting money.</summary>
    public double Money { get; init; }

    /// <summary>The player's starting hacking level.</summary>
    public int HackingLevel { get; init; } = 1;

    public BalanceConfig? Balance { get; init; }

    /// <summary>The servers in the network. The first one is the player's home. Defaults to only <see cref="TestServers.Home"/>.</summary>
    public IReadOnlyList<Server>? Servers { get; init; }

    /// <summary>Defaults to connecting home to every other server.</summary>
    public IReadOnlyList<(string First, string Second)>? Connections { get; init; }

    /// <summary>Defaults to a <see cref="ScriptedRandom"/> whose rolls always succeed.</summary>
    public IRandomSource? Random { get; init; }

    public ActivityCatalog? Catalog { get; init; }
}

/// <summary>
/// A small, fully wired game for Core tests: state, a fake clock and scheduler, formulas and (created on first use)
/// the services and systems over them.
/// </summary>
internal sealed class CoreTestWorld
{
    private readonly Lazy<ServerActions> serverActions;
    private readonly Lazy<NetworkService> networkService;
    private readonly Lazy<BotnetService> botnetService;
    private readonly Lazy<ActivityService> activityService;
    private readonly Lazy<BotnetSystem> botnetSystem;
    private readonly Lazy<ActivitySystem> activitySystem;

    public CoreTestWorld(GameState state, BalanceConfig balance, IRandomSource random, ActivityCatalog catalog)
    {
        State = state;
        Balance = balance;
        Random = random;
        Catalog = catalog;

        Clock = new FakeGameClock();
        Scheduler = new GameScheduler(Clock);
        Output = new RecordingOutput();

        SkillFormulas = new SkillFormulas(balance);
        HackingFormulas = new ServerOperationFormulas(balance, SkillFormulas);
        BotnetFormulas = new BotnetFormulas(balance);
        NetworkFormulas = new NetworkFormulas(balance);
        ActivityFormulas = new ActivityFormulas(balance);

        serverActions = new(() => new ServerActions(State, Scheduler, HackingFormulas, SkillFormulas, Random));
        networkService = new(() => new NetworkService(State, Scheduler, NetworkFormulas));
        botnetService = new(() => new BotnetService(State, BotnetFormulas));
        activityService = new(() => new ActivityService(State, Catalog, ActivityFormulas));
        botnetSystem = new(() => new BotnetSystem(State, BotnetFormulas));
        activitySystem = new(() => new ActivitySystem(State, ActivityFormulas, ActivityService, Random));
    }

    public GameState State { get; }
    public Player Player => State.Player;
    public Network Network => State.Network;
    public BalanceConfig Balance { get; }
    public IRandomSource Random { get; }
    public ActivityCatalog Catalog { get; }

    public FakeGameClock Clock { get; }
    public GameScheduler Scheduler { get; }
    public RecordingOutput Output { get; }

    public ServerOperationFormulas HackingFormulas { get; }
    public BotnetFormulas BotnetFormulas { get; }
    public NetworkFormulas NetworkFormulas { get; }
    public SkillFormulas SkillFormulas { get; }
    public ActivityFormulas ActivityFormulas { get; }

    public ServerActions ServerActions => serverActions.Value;
    public NetworkService NetworkService => networkService.Value;
    public BotnetService BotnetService => botnetService.Value;
    public ActivityService ActivityService => activityService.Value;
    public BotnetSystem BotnetSystem => botnetSystem.Value;
    public ActivitySystem ActivitySystem => activitySystem.Value;

    public Server GetServer(string hostname) => Network.GetServer(hostname);

    /// <summary>Moves game time forward and lets the scheduler complete whatever has become due.</summary>
    public void Advance(TimeSpan amount)
    {
        Clock.Advance(amount);
        Scheduler.Update();
    }

    /// <summary>Completes the one delay a running action is waiting on, however long it is.</summary>
    public void CompleteScheduledAction()
    {
        Assert.Equal(1, Scheduler.PendingCount);

        Advance(TimeSpan.FromDays(1));
    }

    /// <summary>Creates the scripting API for a script running on <paramref name="hostname"/>.</summary>
    public NullByteApi CreateApi(
        IScriptOperations scriptOperations,
        string hostname = TestServers.HomeHostname,
        int threads = 1,
        CancellationToken cancellationToken = default)
        => new(
            State,
            Scheduler,
            networkService.Value,
            botnetService.Value,
            serverActions.Value,
            activityService.Value,
            SkillFormulas,
            BotnetFormulas,
            scriptOperations,
            Output,
            GetServer(hostname).Id,
            threads,
            cancellationToken);
}

/// <summary>Entry point for Core tests. Console tests have <c>ConsoleTestSetup</c>; this is its Core counterpart.</summary>
internal static class CoreTestSetup
{
    public static CoreTestWorld CreateWorld(CoreTestOptions? options = null)
    {
        options ??= new CoreTestOptions();

        var balance = options.Balance ?? new BalanceConfig();
        var servers = options.Servers ?? new[] { TestServers.Home() };
        var home = servers[0];

        var network = new Network();
        foreach (var server in servers)
            network.AddServer(server);

        if (options.Connections is { } connections)
        {
            foreach (var (first, second) in connections)
                network.Connect(first, second);
        }
        else
        {
            foreach (var server in servers.Skip(1))
                network.Connect(home.Hostname, server.Hostname);
        }

        var experience = new SkillFormulas(balance).CalculateExperienceForLevel(SkillKind.Hacking, options.HackingLevel);
        var player = new Player(home.Id, options.Money, experience);

        return new CoreTestWorld(
            new GameState(player, network),
            balance,
            options.Random ?? new ScriptedRandom(),
            options.Catalog ?? ActivityCatalogFactory.CreateDefault());
    }
}
