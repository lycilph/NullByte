using NullByte.Core.Randomness;
using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Services;

public sealed class ServerActionsSimulationTests
{
    [Fact]
    public async Task HackAsync_OneThousandDeterministicHacks_PreservesGameInvariants()
    {
        const int hackCount = 1_000;

        var target = TestServers.Create(
            "target",
            maximumMoney: 1_000_000,
            startingSecurity: 2,
            requiredHackingLevel: 10,
            growthParameter: 20,
            hasRootAccess: true);

        var world = CoreTestSetup.CreateWorld(new CoreTestOptions
        {
            HackingLevel = 10,
            Servers = [TestServers.Home(), target],
            Random = new SeededRandomSource(1337)
        });

        var player = world.Player;
        var formulas = world.HackingFormulas;
        var scheduler = world.Scheduler;
        var actions = world.ServerActions;

        var successes = 0;
        var failures = 0;

        for (var i = 0; i < hackCount; i++)
        {
            var duration = formulas.CalculateHackDuration(player, target);

            var action = actions.HackAsync(target.Hostname, 1, TestContext.Current.CancellationToken);

            Assert.False(action.IsCompleted);
            Assert.Equal(1, scheduler.PendingCount);

            world.Advance(duration);

            var result = await action;

            if (result.Succeeded)
                successes++;
            else
                failures++;

            Assert.InRange(target.Money, 0, target.MaximumMoney);
            Assert.True(target.Security >= target.MinimumSecurity);
            Assert.True(player.Money >= 0);
            Assert.True(player.Skills.GetExperience(SkillKind.Hacking) >= 0);

            Assert.True(double.IsFinite(target.Money));
            Assert.True(double.IsFinite(target.Security));
            Assert.True(double.IsFinite(player.Money));
            Assert.True(double.IsFinite(player.Skills.GetExperience(SkillKind.Hacking)));

            Assert.Equal(0, scheduler.PendingCount);
        }

        Assert.Equal(hackCount, successes + failures);

        Assert.True(successes > 0);
        Assert.True(failures > 0);

        Assert.True(player.Money > 0);
        Assert.True(player.Skills.GetExperience(SkillKind.Hacking) > 0);

        Assert.InRange(target.Money, 0, target.MaximumMoney);
        Assert.True(target.Security >= target.MinimumSecurity);
    }
}