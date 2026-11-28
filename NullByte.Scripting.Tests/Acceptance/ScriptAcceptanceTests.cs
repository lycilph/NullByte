using NSubstitute;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Core.World;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Acceptance;

public sealed class ScriptAcceptanceTests
{
    [Fact]
    public async Task PlayerScript_CompilesAndHacksRealServer()
    {
        var balance = new BalanceConfig();
        var skillFormulas = new SkillFormulas(balance);
        var formulas = new ServerOperationFormulas(balance, skillFormulas);
        var botnetFormulas = new BotnetFormulas(balance);
        var networkFormulas = new NetworkFormulas(balance);
        var activityFormulas = new ActivityFormulas(balance);

        var home = TestServers.Create("home");

        var player = new Player(
            home.Id,
            startingMoney: 1_000,
            startingHackingExperience: skillFormulas.CalculateExperienceForLevel(SkillKind.Hacking, 1));

        var target = TestServers.Create("n00dles", maximumMoney: 1_000_000, startingMoney: 1_000_000, growthParameter: 20);

        var network = new Network();
        network.AddServer(home);
        network.AddServer(target);
        network.Connect(home.Hostname, target.Hostname);

        var gameState = new GameState(player, network);
        gameState.Narrative.Mode = NarrativeProgressionMode.Freeplay;
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        // Zero guarantees that the hack succeeds.
        var randomSource = Substitute.For<IRandomSource>();
        randomSource.NextDouble().Returns(0.0);

        var networkServices = new NetworkService(gameState, scheduler, networkFormulas);
        var botnetService = new BotnetService(gameState, botnetFormulas);
        var cloudServerService = new CloudServerService(gameState, new CloudServerFormulas(balance));
        var actions = new ServerActions(
            gameState,
            scheduler,
            formulas,
            skillFormulas,
            randomSource);
        var activityService = new ActivityService(gameState, ActivityCatalogFactory.CreateDefault(), activityFormulas);

        var output = new TestScriptOutput();

        var args = new[] { "n00dles" };

        var scriptOperations = Substitute.For<IScriptOperations>();
        var api = new NullByteApi(
            gameState,
            scheduler,
            networkServices,
            botnetService,
            cloudServerService,
            actions,
            activityService,
            skillFormulas,
            botnetFormulas,
            scriptOperations,
            output,
            home.Id);

        const string source =
            """
            var target = args[0];
            nb.Print($"Hacking {target}");
            nb.Print($"Cloud servers: {nb.Cloud.GetServers().Count}");
            await nb.Hack(target);
            nb.Print("Hack complete");
            """;

        var script = TestScripts.CompileOrFail(source, "acceptance-test.cx");

        var runner = new ScriptRunner(new ScriptAssemblyLoader());

        var initialPlayerMoney = player.Money;
        var initialServerMoney = target.Money;

        var runTask = runner.RunAsync(script, api, args);

        Assert.False(runTask.IsCompleted);
        Assert.Equal(["Hacking n00dles", "Cloud servers: 0"], output.Lines);

        var hackDuration = formulas.CalculateHackDuration(player, target);

        clock.Advance(hackDuration);
        scheduler.Update();

        await runTask;

        Assert.True(player.Money > initialPlayerMoney);
        Assert.True(target.Money < initialServerMoney);

        Assert.Equal(["Hacking n00dles", "Cloud servers: 0", "Hack complete"], output.Lines);
    }

    private sealed class TestScriptOutput : IGameOutput
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine() { }
    }
}