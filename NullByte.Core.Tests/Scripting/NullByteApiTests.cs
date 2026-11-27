using NSubstitute;
using NullByte.Core.Domain;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Scripting;

public sealed class NullByteApiTests
{
    [Theory]
    [InlineData("hello", "hello")]
    [InlineData(42, "42")]
    [InlineData(null, "")]
    public void Print_WritesTheValueAsTextToTheOutput(object? value, string expected)
    {
        var fixture = new ApiFixture();

        fixture.Api.Print(value);

        Assert.Equal(new[] { expected }, fixture.World.Output.Lines);
    }

    [Fact]
    public async Task Sleep_CompletesAfterTheGivenGameTime()
    {
        var fixture = new ApiFixture();

        var sleep = fixture.Api.Sleep(TimeSpan.FromSeconds(5));

        Assert.False(sleep.IsCompleted);

        fixture.World.Advance(TimeSpan.FromSeconds(4));

        Assert.False(sleep.IsCompleted);

        fixture.World.Advance(TimeSpan.FromSeconds(1));

        await sleep;

        Assert.True(sleep.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Sleep_WhenCanceled_CancelsTheDelay()
    {
        var fixture = new ApiFixture();
        using var cancellationSource = new CancellationTokenSource();
        var api = fixture.CreateApi(cancellationSource.Token);

        var sleep = api.Sleep(TimeSpan.FromSeconds(5));

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sleep);
    }

    public static TheoryData<string, Func<NullByteApi, object>, Func<Server, object>> ServerGetters => new()
    {
        { "money", api => api.GetServerMoneyAvailable("n00dles"), server => server.Money },
        { "max money", api => api.GetServerMaxMoney("n00dles"), server => server.MaximumMoney },
        { "security", api => api.GetServerSecurityLevel("n00dles"), server => server.Security },
        { "min security", api => api.GetServerMinSecurityLevel("n00dles"), server => server.MinimumSecurity },
        { "required hacking level", api => api.GetServerRequiredHackingLevel("n00dles"), server => server.RequiredHackingLevel },
        { "max ram", api => api.GetServerMaxRam("n00dles"), server => server.MaximumRam },
        { "used ram", api => api.GetServerUsedRam("n00dles"), server => server.UsedRam },
        { "root access", api => api.HasRootAccess("n00dles"), server => server.HasRootAccess }
    };

    [Theory]
    [MemberData(nameof(ServerGetters))]
    public void ServerGetter_ReturnsTheServersValue(
        string name,
        Func<NullByteApi, object> read,
        Func<Server, object> expected)
    {
        var fixture = new ApiFixture();

        var actual = read(fixture.Api);
        var expectedValue = expected(fixture.Target);

        Assert.True(Equals(expectedValue, actual), $"{name}: expected {expectedValue} but got {actual}.");
    }

    [Fact]
    public void ServerGetter_WithUnknownHostname_Throws()
    {
        var fixture = new ApiFixture();

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Api.GetServerMoneyAvailable("missing"));

        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Player_ExposesMoney()
    {
        var fixture = new ApiFixture();

        Assert.Equal(fixture.World.Player.Money, fixture.Api.Player.Money);
    }

    [Fact]
    public void Player_ExposesExperienceAndLevelForEverySkill()
    {
        var fixture = new ApiFixture();

        fixture.World.Player.Skills.AddExperience(SkillKind.Hacking, 123.5);
        fixture.World.Player.Skills.AddExperience(SkillKind.Strength, 50);
        fixture.World.Player.Skills.AddExperience(SkillKind.Defense, 25);
        fixture.World.Player.Skills.AddExperience(SkillKind.Dexterity, 10);
        fixture.World.Player.Skills.AddExperience(SkillKind.Agility, 5);
        fixture.World.Player.Skills.AddExperience(SkillKind.Charisma, 2);

        AssertSkill(fixture, fixture.Api.Player.Skills.Hacking, SkillKind.Hacking);
        AssertSkill(fixture, fixture.Api.Player.Skills.Strength, SkillKind.Strength);
        AssertSkill(fixture, fixture.Api.Player.Skills.Defense, SkillKind.Defense);
        AssertSkill(fixture, fixture.Api.Player.Skills.Dexterity, SkillKind.Dexterity);
        AssertSkill(fixture, fixture.Api.Player.Skills.Agility, SkillKind.Agility);
        AssertSkill(fixture, fixture.Api.Player.Skills.Charisma, SkillKind.Charisma);
    }

    [Fact]
    public void The_nb_object_has_a_player_property()
    {
        Assert.NotNull(typeof(INullByteApi).GetProperty(nameof(INullByteApi.Player)));
    }

    private static void AssertSkill(
        ApiFixture fixture,
        INullByteSkillApi skill,
        SkillKind kind)
    {
        var expectedExperience = fixture.World.Player.Skills.GetExperience(kind);
        var expectedLevel = fixture.World.SkillFormulas.CalculateLevel(kind, expectedExperience);

        Assert.Equal(expectedExperience, skill.Experience);
        Assert.Equal(expectedLevel, skill.Level);
    }

    [Fact]
    public async Task Hack_RunsTheHackThroughServerActions()
    {
        var fixture = new ApiFixture();
        var moneyBefore = fixture.Target.Money;
        var playerMoneyBefore = fixture.World.Player.Money;

        var hack = fixture.Api.Hack("n00dles");

        fixture.World.CompleteScheduledAction();

        var result = await hack;

        Assert.True(result.Succeeded);
        Assert.True(result.MoneyStolen > 0);
        Assert.Equal(moneyBefore - result.MoneyStolen, fixture.Target.Money, precision: 10);
        Assert.Equal(playerMoneyBefore + result.MoneyStolen, fixture.World.Player.Money, precision: 10);
    }

    [Fact]
    public async Task Grow_RunsTheGrowThroughServerActions()
    {
        var fixture = new ApiFixture();
        var moneyBefore = fixture.Target.Money;

        var grow = fixture.Api.Grow("n00dles");

        fixture.World.CompleteScheduledAction();

        var result = await grow;

        Assert.True(result.Succeeded);
        Assert.True(result.MoneyGrown > 0);
        Assert.Equal(moneyBefore + result.MoneyGrown, fixture.Target.Money, precision: 10);
    }

    [Fact]
    public async Task Weaken_RunsTheWeakenThroughServerActions()
    {
        var fixture = new ApiFixture();
        var securityBefore = fixture.Target.Security;

        var weaken = fixture.Api.Weaken("n00dles");

        fixture.World.CompleteScheduledAction();

        var result = await weaken;

        Assert.True(result.Succeeded);
        Assert.True(result.SecurityDecrease > 0);
        Assert.Equal(securityBefore - result.SecurityDecrease, fixture.Target.Security, precision: 10);
    }

    [Fact]
    public void Nuke_GainsRootAccessOnTheScriptsHost()
    {
        var fixture = new ApiFixture();
        var api = fixture.CreateApi(hostname: "foodnstuff");

        Assert.False(fixture.NukeTarget.HasRootAccess);

        var result = api.Nuke();

        Assert.True(result.Succeeded);
        Assert.True(fixture.NukeTarget.HasRootAccess);
    }

    [Fact]
    public void Nuke_WhenAlreadyRooted_Fails()
    {
        var fixture = new ApiFixture();
        var api = fixture.CreateApi(hostname: "n00dles");

        var result = api.Nuke();

        Assert.False(result.Succeeded);
        Assert.Contains("already have root access", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // The script-process calls below only delegate to IScriptOperations, so that collaborator is mocked.

    [Fact]
    public void Ps_ListsTheProcessesOnTheScriptsHost()
    {
        var fixture = new ApiFixture();
        var processes = new[]
        {
            new ScriptProcessInfo(42, "hack.cs", ["n00dles"], 2, "home", ScriptProcessState.Running)
        };
        fixture.ScriptOperations.ProcessesRunning("home").Returns(processes);

        var result = fixture.Api.Ps();

        Assert.Same(processes, result);
        fixture.ScriptOperations.Received(1).ProcessesRunning("home");
    }

    [Fact]
    public void Ps_WithHostname_ListsTheProcessesOnThatHost()
    {
        var fixture = new ApiFixture();
        var processes = Array.Empty<ScriptProcessInfo>();
        fixture.ScriptOperations.ProcessesRunning("n00dles").Returns(processes);

        var result = fixture.Api.Ps("n00dles");

        Assert.Same(processes, result);
        fixture.ScriptOperations.Received(1).ProcessesRunning("n00dles");
    }

    [Fact]
    public async Task RunAsync_StartsTheScriptOnTheCurrentHostWithTheProcessThreads()
    {
        var fixture = new ApiFixture();
        var api = fixture.CreateApi(threads: 4);
        fixture.ScriptOperations.StartSimple("home", "hack.cs", Arg.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "n00dles", "10" })), 4).Returns(Task.FromResult(123));

        var pid = await api.RunAsync("hack.cs", "n00dles", "10");

        Assert.Equal(123, pid);
        await fixture.ScriptOperations.Received(1).StartSimple("home", "hack.cs", Arg.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "n00dles", "10" })), 4);
    }

    [Fact]
    public async Task ExecAsync_StartsTheScriptOnTheGivenHost()
    {
        var fixture = new ApiFixture();
        var api = fixture.CreateApi(threads: 3);
        fixture.ScriptOperations.StartSimple("n00dles", "hack.cs", Arg.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "--once" })), 3).Returns(Task.FromResult(456));

        var pid = await api.ExecAsync("hack.cs", "n00dles", "--once");

        Assert.Equal(456, pid);
        await fixture.ScriptOperations.Received(1).StartSimple("n00dles", "hack.cs", Arg.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "--once" })), 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Kill_ReturnsTheResultFromScriptOperations(bool killed)
    {
        var fixture = new ApiFixture();
        fixture.ScriptOperations.Kill(42).Returns(killed);

        var result = fixture.Api.Kill(42);

        Assert.Equal(killed, result);
        fixture.ScriptOperations.Received(1).Kill(42);
    }

    [Fact]
    public void KillAll_ReturnsTheNumberOfKilledProcesses()
    {
        var fixture = new ApiFixture();
        fixture.ScriptOperations.KillAll().Returns(7);

        var result = fixture.Api.KillAll();

        Assert.Equal(7, result);
        fixture.ScriptOperations.Received(1).KillAll();
    }

    [Fact]
    public void ScriptRunning_UsesTheCurrentHostByDefault()
    {
        var fixture = new ApiFixture();
        fixture.ScriptOperations.ScriptRunning("hack.cs", "home").Returns(true);

        var result = fixture.Api.ScriptRunning("hack.cs");

        Assert.True(result);
        fixture.ScriptOperations.Received(1).ScriptRunning("hack.cs", "home");
    }

    [Fact]
    public void ScriptRunning_WithHostname_UsesThatHost()
    {
        var fixture = new ApiFixture();
        fixture.ScriptOperations.ScriptRunning("hack.cs", "n00dles").Returns(false);

        var result = fixture.Api.ScriptRunning("hack.cs", "n00dles");

        Assert.False(result);
        fixture.ScriptOperations.Received(1).ScriptRunning("hack.cs", "n00dles");
    }

    [Fact]
    public void GetScriptRam_UsesTheCurrentHostByDefault()
    {
        var fixture = new ApiFixture();
        fixture.ScriptOperations.GetScriptRam("hack.cs", "home").Returns(2.5);

        var result = fixture.Api.GetScriptRam("hack.cs");

        Assert.Equal(2.5, result);
        fixture.ScriptOperations.Received(1).GetScriptRam("hack.cs", "home");
    }

    [Fact]
    public void GetScriptRam_WithHostname_UsesThatHost()
    {
        var fixture = new ApiFixture();
        fixture.ScriptOperations.GetScriptRam("hack.cs", "n00dles").Returns(3.75);

        var result = fixture.Api.GetScriptRam("hack.cs", "n00dles");

        Assert.Equal(3.75, result);
        fixture.ScriptOperations.Received(1).GetScriptRam("hack.cs", "n00dles");
    }

    /// <summary>A world with a rooted target and an unrooted one, plus a mocked <see cref="IScriptOperations"/>.</summary>
    private sealed class ApiFixture
    {
        private NullByteApi? api;

        public ApiFixture()
        {
            Target = TestServers.Create(
                "n00dles",
                maximumMoney: 50_000,
                startingMoney: 25_000,
                startingSecurity: 1.5,
                growthParameter: 30,
                hasRootAccess: true);

            NukeTarget = TestServers.Create(
                "foodnstuff",
                maximumMoney: 25_000,
                startingMoney: 10_000,
                startingSecurity: 1.5,
                growthParameter: 30);

            World = CoreTestSetup.CreateWorld(new CoreTestOptions
            {
                Money = 1_000,
                Servers = [TestServers.Home(), Target, NukeTarget],
                Connections = [(TestServers.HomeHostname, NukeTarget.Hostname)],
                Random = new SeededRandomSource(1)
            });

            ScriptOperations = Substitute.For<IScriptOperations>();
        }

        public CoreTestWorld World { get; }
        public Server Target { get; }
        public Server NukeTarget { get; }
        public IScriptOperations ScriptOperations { get; }

        /// <summary>The API for a one-thread script on home.</summary>
        public NullByteApi Api => api ??= CreateApi();

        public NullByteApi CreateApi(
            CancellationToken? cancellationToken = null,
            int threads = 1,
            string hostname = TestServers.HomeHostname)
            => World.CreateApi(
                ScriptOperations,
                hostname,
                threads,
                cancellationToken ?? TestContext.Current.CancellationToken);
    }
}
