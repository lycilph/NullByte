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
        var api = fixture.CreateApi();

        api.Print("hello");

        Assert.Equal(["hello"], fixture.Output.Lines);
    }

    [Fact]
    public void Print_ConvertsValueToString()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

        api.Print(42);

        Assert.Equal(["42"], fixture.Output.Lines);
    }

    [Fact]
    public void Print_WithNull_WritesEmptyLine()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

        api.Print(null);

        Assert.Equal([string.Empty], fixture.Output.Lines);
    }

    [Fact]
    public async Task Sleep_CompletesAfterSpecifiedGameTime()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

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
        var api = fixture.CreateApi();
        using var cancellationSource = new CancellationTokenSource();

        var sleep = api.Sleep(TimeSpan.FromSeconds(5), cancellationSource.Token);

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sleep);
    }

    [Fact]
    public void GetServerMoneyAvailable_ReturnsCurrentMoney()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

        Assert.Equal(fixture.Target.Money, api.GetServerMoneyAvailable("n00dles"));
    }

    [Fact]
    public void GetServerMaxMoney_ReturnsMaximumMoney()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

        Assert.Equal(fixture.Target.MaximumMoney, api.GetServerMaxMoney("n00dles"));
    }

    [Fact]
    public void GetServerSecurityLevel_ReturnsCurrentSecurity()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

        Assert.Equal(fixture.Target.Security, api.GetServerSecurityLevel("n00dles"));
    }

    [Fact]
    public void GetServerMinSecurityLevel_ReturnsMinimumSecurity()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

        Assert.Equal(fixture.Target.MinimumSecurity, api.GetServerMinSecurityLevel("n00dles"));
    }

    [Fact]
    public void ServerGetter_WithUnknownHostname_Throws()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();

        var exception = Assert.Throws<InvalidOperationException>(() => api.GetServerMoneyAvailable("missing"));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hack_UsesServerActions()
    {
        var fixture = new Fixture();
        var api = fixture.CreateApi();
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
        var api = fixture.CreateApi();
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
        var api = fixture.CreateApi();
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

    private sealed class Fixture
    {
        private readonly FakeGameClock clock = new();
        private readonly GameScheduler scheduler;
        private readonly ServerActions serverActions;

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

            var network = new Network();
            network.AddServer(home);
            network.AddServer(Target);

            var player = new Player(
                homeHostname: home.Hostname,
                startingMoney: 1_000);

            GameState = new GameState(player, network);

            scheduler = new GameScheduler(clock);

            var balance = new BalanceConfig();
            var formulas = new HackingFormulas(balance);

            serverActions = new ServerActions(
                GameState,
                scheduler,
                formulas,
                new SeededRandomSource(1));

            Output = new TestScriptOutput();
        }

        public GameState GameState { get; }

        public Server Target { get; }

        public TestScriptOutput Output { get; }

        public NullByteApi CreateApi(IReadOnlyList<string>? args = null)
        {
            return new NullByteApi(
                GameState,
                scheduler,
                serverActions,
                Output);
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
            double startingSecurity)
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
                hasRootAccess: true);
        }
    }

    private sealed class TestScriptOutput : IGameOutput
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine() { }
    }
}