using NSubstitute;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Scripting;

public sealed class NullByteApiTests
{
    [Fact]
    public void Print_WritesValueToOutput()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        api.Print("hello");

        Assert.Equal(["hello"], fixture.Output.Lines);
    }

    [Fact]
    public void Print_ConvertsValueToString()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        api.Print(42);

        Assert.Equal(["42"], fixture.Output.Lines);
    }

    [Fact]
    public void Print_WithNull_WritesEmptyLine()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        api.Print(null);

        Assert.Equal([string.Empty], fixture.Output.Lines);
    }

    [Fact]
    public async Task Sleep_CompletesAfterSpecifiedGameTime()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        var sleep = api.Sleep(TimeSpan.FromSeconds(5));

        Assert.False(sleep.IsCompleted);

        fixture.Advance(TimeSpan.FromSeconds(4));

        Assert.False(sleep.IsCompleted);

        fixture.Advance(TimeSpan.FromSeconds(1));

        await sleep;

        Assert.True(sleep.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Sleep_WithCancellation_CancelsDelay()
    {
        var fixture = new Fixture();
        using var cancellationSource = new CancellationTokenSource();
        var api = fixture.CreateApi(cancellationSource.Token);

        var sleep = api.Sleep(TimeSpan.FromSeconds(5));

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sleep);
    }

    [Fact]
    public void GetServerMoneyAvailable_ReturnsCurrentMoney()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.Money, api.GetServerMoneyAvailable("n00dles"));
    }

    [Fact]
    public void GetServerMaxMoney_ReturnsMaximumMoney()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.MaximumMoney, api.GetServerMaxMoney("n00dles"));
    }

    [Fact]
    public void GetServerSecurityLevel_ReturnsCurrentSecurity()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.Security, api.GetServerSecurityLevel("n00dles"));
    }

    [Fact]
    public void GetServerMinSecurityLevel_ReturnsMinimumSecurity()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.MinimumSecurity, api.GetServerMinSecurityLevel("n00dles"));
    }

    [Fact]
    public void GetServerRequiredHackingLevel_ReturnsRequiredHackingLevel()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.RequiredHackingLevel, api.GetServerRequiredHackingLevel("n00dles"));
    }

    [Fact]
    public void GetServerMaxRam_ReturnsMaxRam()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.MaximumRam, api.GetServerMaxRam("n00dles"));
    }

    [Fact]
    public void GetServerUsedRam_ReturnsUsedRam()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.UsedRam, api.GetServerUsedRam("n00dles"));
    }

    [Fact]
    public void GetHasRootAccess_ReturnsRootAccess()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Target.HasRootAccess, api.HasRootAccess("n00dles"));
    }

    [Fact]
    public void GetHackingLevel_ReturnsHackingLevel()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        var level = fixture.HackingFormulas.CalculateHackingLevel(fixture.GameState.Player.HackingExperience);

        Assert.Equal(level, api.GetHackingLevel());
    }

    [Fact]
    public void ServerGetter_WithUnknownHostname_Throws()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        var exception = Assert.Throws<InvalidOperationException>(() => api.GetServerMoneyAvailable("missing"));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hack_UsesServerActions()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);
        var moneyBefore = fixture.Target.Money;
        var playerMoneyBefore = fixture.GameState.Player.Money;

        var hack = api.Hack("n00dles");

        fixture.CompleteScheduledAction();

        var result = await hack;

        Assert.True(result.Succeeded);
        Assert.True(result.MoneyStolen > 0);
        Assert.Equal(
            moneyBefore - result.MoneyStolen,
            fixture.Target.Money,
            precision: 10);
        Assert.Equal(
            playerMoneyBefore + result.MoneyStolen,
            fixture.GameState.Player.Money,
            precision: 10);
    }

    [Fact]
    public async Task Grow_UsesServerActions()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);
        var moneyBefore = fixture.Target.Money;

        var grow = api.Grow("n00dles");

        fixture.CompleteScheduledAction();

        var result = await grow;

        Assert.True(result.Succeeded);
        Assert.True(result.MoneyGrown > 0);
        Assert.Equal(
            moneyBefore + result.MoneyGrown,
            fixture.Target.Money,
            precision: 10);
    }

    [Fact]
    public async Task Weaken_UsesServerActions()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);
        var securityBefore = fixture.Target.Security;

        var weaken = api.Weaken("n00dles");

        fixture.CompleteScheduledAction();

        var result = await weaken;

        Assert.True(result.Succeeded);
        Assert.True(result.SecurityDecrease > 0);
        Assert.Equal(
            securityBefore - result.SecurityDecrease,
            fixture.Target.Security,
            precision: 10);
    }
    [Fact]
    public void Ps_ReturnsRunningProcesses()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        var processes = new[]
        { new ScriptProcessInfo(42, "hack.cs", ["n00dles"], 2, "home", ScriptProcessState.Running) };

        fixture.ScriptOperations
            .ProcessesRunning("home")
            .Returns(processes);

        var result = api.Ps();

        Assert.Same(processes, result);
        fixture.ScriptOperations.Received(1).ProcessesRunning("home");
    }

    [Fact]
    public void Ps_WithHostname_UsesSpecifiedHostname()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        var processes = Array.Empty<ScriptProcessInfo>();

        fixture.ScriptOperations
            .ProcessesRunning("n00dles")
            .Returns(processes);

        var result = api.Ps("n00dles");

        Assert.Same(processes, result);
        fixture.ScriptOperations.Received(1).ProcessesRunning("n00dles");
    }

    [Fact]
    public async Task RunAsync_StartsScriptOnCurrentHost()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken, 4);

        fixture.ScriptOperations
            .StartSimple(
                "home",
                "hack.cs",
                Arg.Is<IReadOnlyList<string>>(args =>
                    args.SequenceEqual(new[] { "n00dles", "10" })),
                4)
            .Returns(Task.FromResult(123));

        var pid = await api.RunAsync("hack.cs", "n00dles", "10");

        Assert.Equal(123, pid);

        await fixture.ScriptOperations.Received(1).StartSimple(
            "home",
            "hack.cs",
            Arg.Is<IReadOnlyList<string>>(args =>
                args.SequenceEqual(new[] { "n00dles", "10" })),
            4);
    }

    [Fact]
    public async Task ExecAsync_StartsScriptOnSpecifiedHost()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken, 3);

        fixture.ScriptOperations
            .StartSimple(
                "n00dles",
                "hack.cs",
                Arg.Is<IReadOnlyList<string>>(args =>
                    args.SequenceEqual(new[] { "--once" })),
                3)
            .Returns(Task.FromResult(456));

        var pid = await api.ExecAsync("hack.cs", "n00dles", "--once");

        Assert.Equal(456, pid);

        await fixture.ScriptOperations.Received(1).StartSimple(
            "n00dles",
            "hack.cs",
            Arg.Is<IReadOnlyList<string>>(args =>
                args.SequenceEqual(new[] { "--once" })),
            3);
    }

    [Fact]
    public void Kill_ReturnsResultFromScriptOperations()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        fixture.ScriptOperations.Kill(42).Returns(true);

        var result = api.Kill(42);

        Assert.True(result);
        fixture.ScriptOperations.Received(1).Kill(42);
    }

    [Fact]
    public void Kill_WhenProcessDoesNotExist_ReturnsFalse()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        fixture.ScriptOperations.Kill(42).Returns(false);

        var result = api.Kill(42);

        Assert.False(result);
        fixture.ScriptOperations.Received(1).Kill(42);
    }

    [Fact]
    public void KillAll_ReturnsNumberOfKilledProcesses()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        fixture.ScriptOperations.KillAll().Returns(7);

        var result = api.KillAll();

        Assert.Equal(7, result);
        fixture.ScriptOperations.Received(1).KillAll();
    }

    [Fact]
    public void ScriptRunning_UsesCurrentHostByDefault()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        fixture.ScriptOperations
            .ScriptRunning("hack.cs", "home")
            .Returns(true);

        var result = api.ScriptRunning("hack.cs");

        Assert.True(result);

        fixture.ScriptOperations.Received(1).ScriptRunning(
            "hack.cs",
            "home");
    }

    [Fact]
    public void ScriptRunning_WithHostname_UsesSpecifiedHost()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        fixture.ScriptOperations
            .ScriptRunning("hack.cs", "n00dles")
            .Returns(false);

        var result = api.ScriptRunning("hack.cs", "n00dles");

        Assert.False(result);

        fixture.ScriptOperations.Received(1).ScriptRunning(
            "hack.cs",
            "n00dles");
    }

    [Fact]
    public void GetScriptRam_UsesCurrentHostByDefault()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        fixture.ScriptOperations
            .GetScriptRam("hack.cs", "home")
            .Returns(2.5);

        var result = api.GetScriptRam("hack.cs");

        Assert.Equal(2.5, result);

        fixture.ScriptOperations.Received(1).GetScriptRam(
            "hack.cs",
            "home");
    }

    [Fact]
    public void GetScriptRam_WithHostname_UsesSpecifiedHost()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken);

        fixture.ScriptOperations
            .GetScriptRam("hack.cs", "n00dles")
            .Returns(3.75);

        var result = api.GetScriptRam("hack.cs", "n00dles");

        Assert.Equal(3.75, result);

        fixture.ScriptOperations.Received(1).GetScriptRam(
            "hack.cs",
            "n00dles");
    }

    [Fact]
    public void Nuke_GainsRootAccessOnTargetServer()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken, 1, "foodnstuff");

        Assert.False(fixture.NukeTarget.HasRootAccess);

        var result = api.Nuke();

        Assert.True(result.Succeeded);
        Assert.True(fixture.NukeTarget.HasRootAccess);
    }

    [Fact]
    public void Nuke_WhenAlreadyRooted_ReturnsFailure()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi(TestContext.Current.CancellationToken, 1, "n00dles");

        var result = api.Nuke();

        Assert.False(result.Succeeded);
        Assert.Contains("already have root access", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Fixture
    {
        private readonly FakeGameClock clock = new();
        private readonly GameScheduler scheduler;
        private readonly ServerActions serverActions;
        private readonly NetworkService networkService;
        private readonly BotnetService botnetService;

        public Fixture()
        {
            var home = CreateServer(
                hostname: "home",
                maximumMoney: 0,
                startingMoney: 0,
                minimumSecurity: 1,
                startingSecurity: 1);

            Target = CreateServer(
                hostname: "n00dles",
                maximumMoney: 50_000,
                startingMoney: 25_000,
                minimumSecurity: 1,
                startingSecurity: 1.5);

            NukeTarget = CreateServer(
                hostname: "foodnstuff",
                maximumMoney: 25_000,
                startingMoney: 10_000,
                minimumSecurity: 1,
                startingSecurity: 1.5,
                hasRootAcess: false);

            var network = new Network();
            network.AddServer(home);
            network.AddServer(Target);
            network.AddServer(NukeTarget);

            network.Connect(home.Hostname, NukeTarget.Hostname);
            
            var player = new Player(
                homeHostname: home.Hostname,
                startingMoney: 1_000);

            GameState = new GameState(player, network);

            scheduler = new GameScheduler(clock);

            var balance = new BalanceConfig();
            HackingFormulas = new HackingFormulas(balance);
            BotnetFormulas = new BotnetFormulas(balance);
            var networkFormulas = new NetworkFormulas(balance);

            networkService = new NetworkService(GameState, scheduler, networkFormulas);
            botnetService = new BotnetService(GameState, BotnetFormulas);
            
            ScriptOperations = Substitute.For<IScriptOperations>();

            serverActions = new ServerActions(
                GameState,
                scheduler,
                HackingFormulas,
                new SeededRandomSource(1));

            Output = new TestScriptOutput();
        }

        public GameState GameState { get; }
        public Server Target { get; }
        public Server NukeTarget { get; }
        public HackingFormulas HackingFormulas { get; }
        public BotnetFormulas BotnetFormulas { get; }
        public IScriptOperations ScriptOperations { get; }
        public TestScriptOutput Output { get; }

        public NullByteApi CreateApi(CancellationToken cancellationToken, int threads = 1, string hostname = "home")
        {
            return new NullByteApi(
                GameState,
                scheduler,
                networkService,
                botnetService,
                serverActions,
                HackingFormulas,
                BotnetFormulas,
                ScriptOperations,
                Output,
                hostname,
                threads,
                cancellationToken);
        }

        public void Advance(TimeSpan amount)
        {
            clock.Advance(amount);
            scheduler.Update();
        }

        public void CompleteScheduledAction()
        {
            Assert.Equal(1, scheduler.PendingCount);

            clock.Advance(TimeSpan.FromDays(1));
            scheduler.Update();
        }

        private static Server CreateServer(
            string hostname,
            double maximumMoney,
            double startingMoney,
            double minimumSecurity,
            double startingSecurity,
            bool hasRootAcess = true)
        {
            return new Server(
                hostname,
                maximumMoney,
                startingMoney,
                baseDifficulty: 5,
                minimumSecurity: minimumSecurity,
                startingSecurity: startingSecurity,
                requiredHackingLevel: 1,
                growthParameter: 30,
                maximumRam: 8,
                hasRootAccess: hasRootAcess);
        }
    }

    private sealed class TestScriptOutput : IGameOutput
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine() { }
    }
}