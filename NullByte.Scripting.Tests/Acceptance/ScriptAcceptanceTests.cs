using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests.Acceptance;

public sealed class ScriptAcceptanceTests
{
    [Fact]
    public async Task PlayerScript_CompilesAndHacksRealServer()
    {
        var balance = new BalanceConfig();

        var player = new Player(
            homeHostname: "home",
            startingMoney: 1_000,
            startingHackingExperience: ExperienceForLevel(
                level: 10,
                balance));

        var home = new Server(
            id: "home",
            hostname: "home",
            maximumMoney: 0,
            startingMoney: 0,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter: 0,
            maximumRam: 8,
            hasRootAccess: true);

        var target = new Server(
            id: "n00dles",
            hostname: "n00dles",
            maximumMoney: 1_000_000,
            startingMoney: 1_000_000,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter: 20,
            maximumRam: 8,
            hasRootAccess: true);

        var network = new Network();
        network.AddServer(home);
        network.AddServer(target);
        network.Connect(home.Id, target.Id);

        var gameState = new GameState(player, network);
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);
        var formulas = new HackingFormulas(balance);

        var level = formulas.CalculateHackingLevel(player.HackingExperience);

        // Zero guarantees that the hack succeeds.
        var randomSource = new FixedRandomSource(0);

        var actions = new ServerActions(
            gameState,
            scheduler,
            formulas,
            randomSource);

        var output = new TestScriptOutput();

        var args = new[] { "n00dles" };

        var api = new NullByteApi(
            gameState,
            scheduler,
            actions,
            output);

        const string source =
            """
            var target = args[0];
            nb.Print($"Hacking {target}");
            await nb.Hack(target);
            nb.Print("Hack complete");
            """;

        var compiler = new ScriptCompiler();
        var compilation = compiler.Compile(source, "acceptance-test.cs");

        Assert.True(compilation.Succeeded, FormatDiagnostics(compilation));

        var script = Assert.IsType<CompiledScript>(compilation.Script);

        var runner = new ScriptRunner(new ScriptAssemblyLoader());

        var initialPlayerMoney = player.Money;
        var initialServerMoney = target.Money;

        var runTask = runner.RunAsync(script, api, args);

        Assert.False(runTask.IsCompleted);
        Assert.Equal(["Hacking n00dles"], output.Lines);

        var hackDuration = formulas.CalculateHackDuration(player, target);

        clock.Advance(hackDuration);
        scheduler.Update();

        await runTask;

        Assert.True(player.Money > initialPlayerMoney);
        Assert.True(target.Money < initialServerMoney);

        Assert.Equal(["Hacking n00dles", "Hack complete"], output.Lines);
    }

    private static double ExperienceForLevel(int level, BalanceConfig balance)
    {
        return Math.Pow(level - 1, 2) * balance.HackingLevelExperienceFactor;
    }

    private static string FormatDiagnostics(ScriptCompilationResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}"));
    }

    private sealed class TestScriptOutput : IScriptOutput
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message) => Lines.Add(message);
    }

    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly double value;

        public FixedRandomSource(double value) { this.value = value;}

        public double NextDouble() => value;
    }
}