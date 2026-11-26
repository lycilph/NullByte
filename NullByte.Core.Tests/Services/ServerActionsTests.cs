using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Tests.Infrastructure;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Services;

public sealed class ServerActionsTests
{
    [Fact]
    public async Task HackAsync_DoesNotCompleteBeforeDuration()
    {
        var fixture = CreateFixture(randomValue: 0);

        var duration = fixture.Formulas.CalculateHackDuration(fixture.Player, fixture.Target);
        var action = fixture.Actions.HackAsync(fixture.Target.Hostname, 1, TestContext.Current.CancellationToken);

        fixture.World.Advance(duration - TimeSpan.FromTicks(1));

        Assert.False(action.IsCompleted);
        Assert.Equal(50_000, fixture.Target.Money);
        Assert.Equal(1_000, fixture.Player.Money);

        fixture.World.Advance(TimeSpan.FromTicks(1));

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

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname, 1, TestContext.Current.CancellationToken);

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

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname, threads, TestContext.Current.CancellationToken);
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
         * With hacking level 10, required level 10, and security 2,
         * the Bitburner chance formula gives approximately 0.42.
         * A roll of 0.75 therefore fails.
         */
        var initialServerMoney = fixture.Target.Money;
        var initialPlayerMoney = fixture.Player.Money;

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname, 1, TestContext.Current.CancellationToken);
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
        var expectedExperience = fixture.Formulas.CalculateHackingExperience(fixture.Target) * fixture.Formulas.GetFailedHackExperienceFactor();

        var action = fixture.Actions.HackAsync(fixture.Target.Hostname, 1, TestContext.Current.CancellationToken);
        await CompleteHackAsync(fixture, action);

        var result = await action;

        Assert.False(result.Succeeded);

        Assert.Equal(
            initialSecurity + expectedSecurityIncrease,
            fixture.Target.Security,
            precision: 2);

        Assert.Equal(
            initialExperience + expectedExperience,
            fixture.Player.HackingExperience,
            precision: 2);
    }

    [Fact]
    public async Task HackAsync_MultipleThreads_ScaleEffectsButNotDuration()
    {
        var singleThreadFixture = CreateFixture(randomValue: 0);

        var multiThreadFixture = CreateFixture(randomValue: 0);

        var singleThreadAction =
            singleThreadFixture.Actions.HackAsync(
                singleThreadFixture.Target.Hostname,
                threads: 1,
                TestContext.Current.CancellationToken);

        var multiThreadAction =
            multiThreadFixture.Actions.HackAsync(
                multiThreadFixture.Target.Hostname,
                threads: 5, 
                TestContext.Current.CancellationToken);

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
    public async Task HackAsync_WithoutRootAccess_FailsWithMessage()
    {
        var fixture = CreateFixture(randomValue: 0, hasRootAccess: false);

        var action = await fixture.Actions.HackAsync(fixture.Target.Hostname, 1, TestContext.Current.CancellationToken);

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

        var action = await fixture.Actions.HackAsync(fixture.Target.Hostname, 1, TestContext.Current.CancellationToken);

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
            () => fixture.Actions.HackAsync(fixture.Target.Hostname, threads, TestContext.Current.CancellationToken));

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

        var action = fixture.Actions.GrowAsync(fixture.Target.Hostname, threads, TestContext.Current.CancellationToken);
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

        var action = fixture.Actions.GrowAsync(fixture.Target.Hostname, threads: 100, TestContext.Current.CancellationToken);
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

        var action = fixture.Actions.GrowAsync(fixture.Target.Hostname, 1, TestContext.Current.CancellationToken);
        await CompleteGrowAsync(fixture, action);

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

        var action = fixture.Actions.WeakenAsync(fixture.Target.Hostname, threads, TestContext.Current.CancellationToken);
        await CompleteWeakenAsync(fixture, action);

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

        var action = fixture.Actions.WeakenAsync(fixture.Target.Hostname, threads: 100, TestContext.Current.CancellationToken);
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
    public async Task BackdoorAsync_DoesNotCompleteBeforeDuration()
    {
        var fixture = CreateFixture(randomValue: 0);

        var duration = fixture.Formulas.CalculateBackdoorDuration(fixture.Player, fixture.Target);
        var action = fixture.Actions.BackdoorAsync(fixture.Target.Hostname, TestContext.Current.CancellationToken);

        fixture.World.Advance(duration - TimeSpan.FromTicks(1));

        Assert.False(action.IsCompleted);
        Assert.False(fixture.Target.HasBackdoor);

        fixture.World.Advance(TimeSpan.FromTicks(1));

        var result = await action;

        Assert.True(result.Succeeded);
        Assert.True(fixture.Target.HasBackdoor);
    }

    [Fact]
    public async Task BackdoorAsync_TakesAsLongAsHacking()
    {
        var fixture = CreateFixture(randomValue: 0);

        var hackDuration = fixture.Formulas.CalculateHackDuration(fixture.Player, fixture.Target);
        var action = fixture.Actions.BackdoorAsync(fixture.Target.Hostname, TestContext.Current.CancellationToken);

        fixture.World.Advance(hackDuration);

        var result = await action;

        Assert.Equal(hackDuration, result.Duration);
    }

    [Fact]
    public async Task BackdoorAsync_DoesNotChangeMoneySecurityOrExperience()
    {
        var fixture = CreateFixture(randomValue: 0);

        var initialServerMoney = fixture.Target.Money;
        var initialPlayerMoney = fixture.Player.Money;
        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;

        var action = fixture.Actions.BackdoorAsync(fixture.Target.Hostname, TestContext.Current.CancellationToken);
        fixture.World.Advance(fixture.Formulas.CalculateBackdoorDuration(fixture.Player, fixture.Target));
        await action;

        Assert.Equal(initialServerMoney, fixture.Target.Money);
        Assert.Equal(initialPlayerMoney, fixture.Player.Money);
        Assert.Equal(initialSecurity, fixture.Target.Security);
        Assert.Equal(initialExperience, fixture.Player.HackingExperience);
    }

    [Fact]
    public async Task BackdoorAsync_WithoutRootAccess_FailsWithMessage()
    {
        var fixture = CreateFixture(randomValue: 0, hasRootAccess: false);

        var result = await fixture.Actions.BackdoorAsync(fixture.Target.Hostname, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
        Assert.False(fixture.Target.HasBackdoor);
        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    [Fact]
    public async Task BackdoorAsync_WhenHackingLevelIsTooLow_FailsWithMessage()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            playerLevel: 5,
            requiredHackingLevel: 10);

        var result = await fixture.Actions.BackdoorAsync(fixture.Target.Hostname, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
        Assert.False(fixture.Target.HasBackdoor);
        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    [Fact]
    public async Task BackdoorAsync_WhenAlreadyInstalled_FailsWithoutScheduling()
    {
        var fixture = CreateFixture(randomValue: 0);
        fixture.Target.InstallBackdoor();

        var result = await fixture.Actions.BackdoorAsync(fixture.Target.Hostname, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("already has a backdoor", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    [Theory]
    [InlineData("hack")]
    [InlineData("grow")]
    [InlineData("weaken")]
    [InlineData("backdoor")]
    public async Task Action_WhenCanceled_DoesNotMutateGameState(string action)
    {
        var fixture = CreateFixture(randomValue: 0, startingSecurity: 5);

        var initialServerMoney = fixture.Target.Money;
        var initialPlayerMoney = fixture.Player.Money;
        var initialSecurity = fixture.Target.Security;
        var initialExperience = fixture.Player.HackingExperience;

        using var cancellationSource = new CancellationTokenSource();

        var task = StartAction(fixture, action, fixture.Target.Hostname, cancellationSource.Token);
        Assert.Equal(1, fixture.Scheduler.PendingCount);

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        Assert.Equal(initialServerMoney, fixture.Target.Money);
        Assert.Equal(initialPlayerMoney, fixture.Player.Money);
        Assert.Equal(initialSecurity, fixture.Target.Security);
        Assert.Equal(initialExperience, fixture.Player.HackingExperience);
        Assert.False(fixture.Target.HasBackdoor);
    }

    [Theory]
    [InlineData("hack")]
    [InlineData("grow")]
    [InlineData("weaken")]
    [InlineData("backdoor")]
    public async Task Action_WithUnknownHostname_ThrowsBeforeScheduling(string action)
    {
        var fixture = CreateFixture(randomValue: 0);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => StartAction(fixture, action, "unknown-server", TestContext.Current.CancellationToken));

        Assert.Equal(0, fixture.Scheduler.PendingCount);
    }

    [Fact]
    public void Nuke_WithSufficientHackingLevel_GrantsRootAccess()
    {
        var fixture = CreateFixture(randomValue: 0, hasRootAccess: false);

        var result = fixture.Actions.Nuke(fixture.Target.Hostname);

        Assert.True(result.Succeeded);
        Assert.True(fixture.Target.HasRootAccess);
    }

    [Fact]
    public void Nuke_WhenHackingLevelIsTooLow_FailsAndSaysWhatWasRequired()
    {
        var fixture = CreateFixture(
            randomValue: 0,
            playerLevel: 5,
            requiredHackingLevel: 10,
            hasRootAccess: false);

        var result = fixture.Actions.Nuke(fixture.Target.Hostname);

        Assert.False(result.Succeeded);
        Assert.False(fixture.Target.HasRootAccess);
        Assert.Contains("required 10", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Nuke_WhenAlreadyRooted_Fails()
    {
        var fixture = CreateFixture(randomValue: 0, hasRootAccess: true);

        var result = fixture.Actions.Nuke(fixture.Target.Hostname);

        Assert.False(result.Succeeded);
        Assert.Contains("already have root access", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static Task StartAction(Fixture fixture, string action, string hostname, CancellationToken cancellationToken)
        => action switch
        {
            "hack" => fixture.Actions.HackAsync(hostname, cancellationToken: cancellationToken),
            "grow" => fixture.Actions.GrowAsync(hostname, cancellationToken: cancellationToken),
            "weaken" => fixture.Actions.WeakenAsync(hostname, cancellationToken: cancellationToken),
            "backdoor" => fixture.Actions.BackdoorAsync(hostname, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

    private static async Task CompleteHackAsync(Fixture fixture, Task<HackResult> action)
    {
        fixture.World.Advance(fixture.Formulas.CalculateHackDuration(fixture.Player, fixture.Target));

        await action;
    }

    private static async Task CompleteGrowAsync(Fixture fixture, Task<GrowResult> action)
    {
        fixture.World.Advance(fixture.Formulas.CalculateGrowDuration(fixture.Player, fixture.Target));

        await action;
    }

    private static async Task CompleteWeakenAsync(Fixture fixture, Task<WeakenResult> action)
    {
        fixture.World.Advance(fixture.Formulas.CalculateWeakenDuration(fixture.Player, fixture.Target));

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
        var target = TestServers.Create(
            "target",
            maximumMoney: maximumMoney,
            startingMoney: startingMoney,
            minimumSecurity: minimumSecurity,
            startingSecurity: startingSecurity,
            requiredHackingLevel: requiredHackingLevel,
            growthParameter: growthParameter,
            hasRootAccess: hasRootAccess);

        var world = CoreTestSetup.CreateWorld(new CoreTestOptions
        {
            Money = 1_000,
            HackingLevel = playerLevel,
            Servers = [TestServers.Home(), target],
            Random = TestRandom.Always(randomValue)
        });

        return new Fixture(world, target);
    }

    private sealed record Fixture(CoreTestWorld World, Server Target)
    {
        public Player Player => World.Player;
        public GameScheduler Scheduler => World.Scheduler;
        public HackingFormulas Formulas => World.HackingFormulas;
        public ServerActions Actions => World.ServerActions;
    }
}
