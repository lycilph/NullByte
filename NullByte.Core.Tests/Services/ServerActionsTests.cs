using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Core.Tests.TestDoubles;

namespace NullByte.Core.Tests.Services;

public sealed class ServerActionsTests
{
    [Fact]
    public async Task HackAsync_DoesNotCompleteBeforeDuration()
    {
        var fixture = CreateFixture(randomValue: 0);

        var duration = fixture.Formulas.CalculateHackDuration(fixture.Player, fixture.Target);
        var action = fixture.Actions.HackAsync(fixture.Target.Hostname);

        fixture.Clock.Advance(duration - TimeSpan.FromTicks(1));
        fixture.Scheduler.Update();

        Assert.False(action.IsCompleted);
        Assert.Equal(50_000, fixture.Target.Money);
        Assert.Equal(1_000, fixture.Player.Money);

        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        fixture.Scheduler.Update();

        await action;
    }

    [Fact]
    public async Task HackAsync_SuccessfulHack_TransfersMoney()
    {
        var fixture = CreateFixture(randomValue: 0);

        var initialServerMoney = fixture.Target.Money;
        var initialPlayerMoney = fixture.Player.Money;

        var expectedStolen =
            fixture.Formulas.CalculateHackAmount(
                fixture.Player,
                fixture.Target,
                threads: 1);

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname);

        await CompleteHackAsync(fixture, action);

        var result = await action;

        Assert.True(result.Succeeded);

        Assert.Equal(
            expectedStolen,
            result.MoneyStolen,
            precision: 10);

        Assert.Equal(
            initialServerMoney - expectedStolen,
            fixture.Target.Money,
            precision: 10);

        Assert.Equal(
            initialPlayerMoney + expectedStolen,
            fixture.Player.Money,
            precision: 10);
    }

    [Fact]
    public async Task HackAsync_SuccessfulHack_IncreasesSecurityAndAwardsExperience()
    {
        var fixture = CreateFixture(randomValue: 0);

        const int threads = 5;

        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;
        var expectedSecurityIncrease = fixture.Formulas.CalculateHackSecurityIncrease(threads);
        var expectedExperience = fixture.Formulas.CalculateHackingExperience(fixture.Target, threads);

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname, threads);
        await CompleteHackAsync(fixture, action);

        var result = await action;

        Assert.Equal(
            expectedSecurityIncrease,
            result.SecurityIncrease,
            precision: 10);

        Assert.Equal(
            initialSecurity + expectedSecurityIncrease,
            fixture.Target.Security,
            precision: 10);

        Assert.Equal(
            expectedExperience,
            result.ExperienceGained,
            precision: 10);

        Assert.Equal(
            initialExperience + expectedExperience,
            fixture.Player.HackingExperience,
            precision: 10);
    }

    [Fact]
    public async Task HackAsync_FailedHack_DoesNotTransferMoney()
    {
        var fixture = CreateFixture(randomValue: 0.75, startingSecurity: 2);

        /*
         * Player level and required level are equal, while current
         * security is twice minimum security. The resulting chance
         * is therefore 0.5, so a roll of 0.75 fails.
         */
        var initialServerMoney = fixture.Target.Money;
        var initialPlayerMoney = fixture.Player.Money;

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname);
        await CompleteHackAsync(fixture, action);

        var result = await action;

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.MoneyStolen);
        Assert.Equal(initialServerMoney, fixture.Target.Money);
        Assert.Equal(initialPlayerMoney, fixture.Player.Money);
    }

    [Fact]
    public async Task HackAsync_FailedHack_StillIncreasesSecurityAndAwardsExperience()
    {
        var fixture = CreateFixture(randomValue: 0.75, startingSecurity: 2);

        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;
        var expectedSecurityIncrease = fixture.Formulas.CalculateHackSecurityIncrease();
        var expectedExperience = fixture.Formulas.CalculateHackingExperience(fixture.Target);

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname);
        await CompleteHackAsync(fixture, action);

        var result = await action;

        Assert.False(result.Succeeded);

        Assert.Equal(
            initialSecurity + expectedSecurityIncrease,
            fixture.Target.Security,
            precision: 10);

        Assert.Equal(
            initialExperience + expectedExperience,
            fixture.Player.HackingExperience,
            precision: 10);
    }

    [Fact]
    public async Task HackAsync_MultipleThreads_ScaleEffectsButNotDuration()
    {
        var singleThreadFixture = CreateFixture(randomValue: 0);

        var multiThreadFixture = CreateFixture(randomValue: 0);

        var singleThreadAction =
            singleThreadFixture.Actions.HackAsync(
                singleThreadFixture.Target.Hostname,
                threads: 1);

        var multiThreadAction =
            multiThreadFixture.Actions.HackAsync(
                multiThreadFixture.Target.Hostname,
                threads: 5);

        await CompleteHackAsync(singleThreadFixture, singleThreadAction);
        await CompleteHackAsync(multiThreadFixture, multiThreadAction);

        var singleThreadResult = await singleThreadAction;
        var multiThreadResult = await multiThreadAction;

        Assert.Equal(singleThreadResult.Duration, multiThreadResult.Duration);

        Assert.Equal(
            singleThreadResult.MoneyStolen * 5,
            multiThreadResult.MoneyStolen,
            precision: 10);

        Assert.Equal(
            singleThreadResult.SecurityIncrease * 5,
            multiThreadResult.SecurityIncrease,
            precision: 10);

        Assert.Equal(
            singleThreadResult.ExperienceGained * 5,
            multiThreadResult.ExperienceGained,
            precision: 10);
    }

    [Fact]
    public async Task HackAsync_WhenCanceled_DoesNotMutateGameState()
    {
        var fixture = CreateFixture(randomValue: 0);

        var initialServerMoney = fixture.Target.Money;
        var initialPlayerMoney = fixture.Player.Money;
        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;

        using var cancellationSource = new CancellationTokenSource();

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname, cancellationToken: cancellationSource.Token);
        Assert.Equal(1, fixture.Scheduler.PendingCount);
        
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await action);

        Assert.Equal(initialServerMoney, fixture.Target.Money);
        Assert.Equal(initialPlayerMoney, fixture.Player.Money);
        Assert.Equal(initialSecurity, fixture.Target.Security);
        Assert.Equal(initialExperience, fixture.Player.HackingExperience);
    }

    [Fact]
    public async Task HackAsync_WithoutRootAccess_FailsWithMessage()
    {
        var fixture = CreateFixture(randomValue: 0, hasRootAccess: false);

        var action = await fixture.Actions.HackAsync(fixture.Target.Hostname);

        Assert.False(action.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(action.FailureMessage));
        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    [Fact]
    public async Task HackAsync_WhenHackingLevelIsTooLow_FailsWithMessage()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            playerLevel: 5,
            requiredHackingLevel: 10);

        var action = await fixture.Actions.HackAsync(fixture.Target.Hostname);

        Assert.False(action.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(action.FailureMessage));
        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HackAsync_WithInvalidThreadCount_Throws(
        int threads)
    {
        var fixture = CreateFixture(randomValue: 0);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fixture.Actions.HackAsync(fixture.Target.Hostname, threads));

        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    [Fact]
    public async Task GrowAsync_IncreasesMoneyAndSecurityAndAwardsExperience()
    {
        var fixture = CreateFixture(randomValue: 0);

        const int threads = 5;

        var initialMoney = fixture.Target.Money;
        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;
        var expectedGrowth = fixture.Formulas.CalculateGrowthAmount(fixture.Target, threads);
        var expectedSecurityIncrease = fixture.Formulas.CalculateGrowSecurityIncrease(threads);
        var expectedExperience = fixture.Formulas.CalculateHackingExperience(fixture.Target, threads);

        var action = fixture.Actions.GrowAsync(fixture.Target.Hostname, threads);
        await CompleteGrowAsync(fixture, action);

        var result = await action;

        Assert.Equal(
            expectedGrowth,
            result.MoneyGrown,
            precision: 10);

        Assert.Equal(
            initialMoney + expectedGrowth,
            fixture.Target.Money,
            precision: 10);

        Assert.Equal(
            initialSecurity + expectedSecurityIncrease,
            fixture.Target.Security,
            precision: 10);

        Assert.Equal(
            initialExperience + expectedExperience,
            fixture.Player.HackingExperience,
            precision: 10);
    }

    [Fact]
    public async Task GrowAsync_CannotIncreaseMoneyAboveMaximum()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            maximumMoney: 100_000,
            startingMoney: 99_000,
            growthParameter: 100);

        var action = fixture.Actions.GrowAsync(fixture.Target.Hostname, threads: 100);
        await CompleteGrowAsync(fixture, action);

        var result = await action;

        Assert.Equal(
            100_000,
            fixture.Target.Money,
            precision: 10);

        Assert.Equal(
            1_000,
            result.MoneyGrown,
            precision: 10);
    }

    [Fact]
    public async Task GrowAsync_WhenAlreadyAtMaximum_ReturnsZeroGrowth()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            maximumMoney: 100_000,
            startingMoney: 100_000);

        var action = fixture.Actions.GrowAsync(fixture.Target.Hostname);
        await CompleteGrowAsync(fixture,action);

        var result = await action;

        Assert.Equal(0, result.MoneyGrown);
        Assert.Equal(100_000, fixture.Target.Money);
    }

    [Fact]
    public async Task WeakenAsync_ReducesSecurityAndAwardsExperience()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            minimumSecurity: 1,
            startingSecurity: 5);

        const int threads = 10;

        var initialExperience = fixture.Player.HackingExperience;
        var expectedExperience = fixture.Formulas.CalculateHackingExperience(fixture.Target, threads);

        var action = fixture.Actions.WeakenAsync(fixture.Target.Hostname, threads);
        await CompleteWeakenAsync(fixture,action);

        var result = await action;

        Assert.Equal(
            0.5,
            result.SecurityDecrease,
            precision: 10);

        Assert.Equal(
            4.5,
            fixture.Target.Security,
            precision: 10);

        Assert.Equal(
            initialExperience + expectedExperience,
            fixture.Player.HackingExperience,
            precision: 10);
    }

    [Fact]
    public async Task WeakenAsync_CannotReduceSecurityBelowMinimum()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            minimumSecurity: 1,
            startingSecurity: 1.2);

        var action = fixture.Actions.WeakenAsync(fixture.Target.Hostname, threads: 100);
        await CompleteWeakenAsync(fixture, action);

        var result = await action;

        Assert.Equal(
            0.2,
            result.SecurityDecrease,
            precision: 10);

        Assert.Equal(
            fixture.Target.MinimumSecurity,
            fixture.Target.Security,
            precision: 10);
    }

    [Fact]
    public async Task GrowAsync_WhenCanceled_DoesNotMutateGameState()
    {
        var fixture = CreateFixture(randomValue: 0);

        var initialMoney = fixture.Target.Money;
        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;

        using var cancellationSource = new CancellationTokenSource();

        var action = fixture.Actions.GrowAsync(fixture.Target.Hostname, cancellationToken: cancellationSource.Token);
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await action);

        Assert.Equal(initialMoney, fixture.Target.Money);
        Assert.Equal(initialSecurity, fixture.Target.Security);
        Assert.Equal(initialExperience, fixture.Player.HackingExperience);
    }

    [Fact]
    public async Task WeakenAsync_WhenCanceled_DoesNotMutateGameState()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            startingSecurity: 5);

        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;

        using var cancellationSource = new CancellationTokenSource();

        var action = fixture.Actions.WeakenAsync(fixture.Target.Hostname, cancellationToken: cancellationSource.Token);
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await action);

        Assert.Equal(initialSecurity, fixture.Target.Security);
        Assert.Equal(initialExperience, fixture.Player.HackingExperience);
    }

    [Fact]
    public async Task Action_WithUnknownHostname_ThrowsBeforeScheduling()
    {
        var fixture = CreateFixture(randomValue: 0);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Actions.HackAsync("unknown-server"));

        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    private static async Task CompleteHackAsync(Fixture fixture, Task<HackResult> action)
    {
        var duration = fixture.Formulas.CalculateHackDuration(fixture.Player, fixture.Target);

        fixture.Clock.Advance(duration);
        fixture.Scheduler.Update();

        await action;
    }

    private static async Task CompleteGrowAsync(Fixture fixture, Task<GrowResult> action)
    {
        var duration = fixture.Formulas.CalculateGrowDuration(fixture.Player, fixture.Target);

        fixture.Clock.Advance(duration);
        fixture.Scheduler.Update();

        await action;
    }

    private static async Task CompleteWeakenAsync(Fixture fixture, Task<WeakenResult> action)
    {
        var duration = fixture.Formulas.CalculateWeakenDuration(fixture.Player, fixture.Target);

        fixture.Clock.Advance(duration);
        fixture.Scheduler.Update();

        await action;
    }

    private static Fixture CreateFixture(
        double randomValue,
        int playerLevel = 10,
        double maximumMoney = 100_000,
        double startingMoney = 50_000,
        double minimumSecurity = 1,
        double startingSecurity = 1,
        int requiredHackingLevel = 10,
        double growthParameter = 20,
        bool hasRootAccess = true)
    {
        var balance = new BalanceConfig();

        var experience =
            Math.Pow(playerLevel - 1, 2) *
            balance.HackingLevelExperienceFactor;

        var player = new Player(
            homeHostname: "home",
            startingMoney: 1_000,
            startingHackingExperience: experience);

        var home = new Server(
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
            hostname: "target",
            maximumMoney: maximumMoney,
            startingMoney: startingMoney,
            minimumSecurity: minimumSecurity,
            startingSecurity: startingSecurity,
            requiredHackingLevel: requiredHackingLevel,
            growthParameter: growthParameter,
            maximumRam: 8,
            hasRootAccess: hasRootAccess);

        var network = new Network();
        network.AddServer(home);
        network.AddServer(target);
        network.Connect(home.Hostname, target.Hostname);

        var gameState = new GameState(player, network);
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);
        var formulas = new HackingFormulas(balance);
        var randomSource = new SequenceRandomSource(randomValue);

        var actions = new ServerActions(
            gameState,
            scheduler,
            formulas,
            randomSource);

        return new Fixture(
            Player: player,
            Target: target,
            Clock: clock,
            Scheduler: scheduler,
            Formulas: formulas,
            Actions: actions);
    }

    private sealed record Fixture(
        Player Player,
        Server Target,
        FakeGameClock Clock,
        GameScheduler Scheduler,
        HackingFormulas Formulas,
        ServerActions Actions);
}