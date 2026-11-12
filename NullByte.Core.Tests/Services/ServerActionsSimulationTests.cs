using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Services;

public sealed class ServerActionsSimulationTests
{
    [Fact]
    public async Task HackAsync_OneThousandDeterministicHacks_PreservesGameInvariants()
    {
        const int hackCount = 1_000;

        var balance = new BalanceConfig();
        var formulas = new HackingFormulas(balance);

        var player = new Player(
            homeHostname: "home",
            startingMoney: 0,
            startingHackingExperience: formulas.CalculateExperienceForHackingLevel(10));

        var home = new Server(
            hostname: "home",
            maximumMoney: 0,
            startingMoney: 0,
            baseDifficulty: 5,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter: 0,
            maximumRam: 8,
            hasRootAccess: true);

        var target = new Server(
            hostname: "target",
            maximumMoney: 1_000_000,
            startingMoney: 1_000_000,
            baseDifficulty: 5,
            minimumSecurity: 1,
            startingSecurity: 2,
            requiredHackingLevel: 10,
            growthParameter: 20,
            maximumRam: 8,
            hasRootAccess: true);

        var network = new Network();

        network.AddServer(home);
        network.AddServer(target);
        network.Connect(home.Hostname, target.Hostname);

        var gameState = new GameState(player, network);

        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);
        var randomSource = new SeededRandomSource(balance.RandomSeed);

        var actions = new ServerActions(
            gameState,
            scheduler,
            formulas,
            randomSource);

        var successes = 0;
        var failures = 0;

        for (var i = 0; i < hackCount; i++)
        {
            var duration = formulas.CalculateHackDuration(player, target);

            var action = actions.HackAsync(target.Hostname);

            Assert.False(action.IsCompleted);
            Assert.Equal(1, scheduler.PendingCount);

            clock.Advance(duration);
            scheduler.Update();

            var result = await action;

            if (result.Succeeded)
                successes++;
            else
                failures++;

            Assert.InRange(target.Money, 0, target.MaximumMoney);
            Assert.True(target.Security >= target.MinimumSecurity);
            Assert.True(player.Money >= 0);
            Assert.True(player.HackingExperience >= 0);

            Assert.True(double.IsFinite(target.Money));
            Assert.True(double.IsFinite(target.Security));
            Assert.True(double.IsFinite(player.Money));
            Assert.True(double.IsFinite(player.HackingExperience));

            Assert.Equal(0, scheduler.PendingCount);
        }

        Assert.Equal(hackCount, successes + failures);

        Assert.True(successes > 0);
        Assert.True(failures > 0);

        Assert.True(player.Money > 0);
        Assert.True(player.HackingExperience > 0);

        Assert.InRange(target.Money, 0, target.MaximumMoney);
        Assert.True(target.Security >= target.MinimumSecurity);
    }
}