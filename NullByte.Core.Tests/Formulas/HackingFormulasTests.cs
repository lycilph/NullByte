using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class HackingFormulasTests
{
    private readonly BalanceConfig balance = new();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(31, 1)]
    [InlineData(32, 2)]
    [InlineData(287, 3)]
    [InlineData(288, 4)]
    [InlineData(800, 6)]
    [InlineData(2_592, 10)]
    public void CalculateHackingLevel_ReturnsExpectedLevel(
        double experience,
        int expectedLevel)
    {
        var formulas = CreateFormulas();

        var level =
            formulas.CalculateHackingLevel(experience);

        Assert.Equal(expectedLevel, level);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CalculateHackingLevel_WithInvalidExperience_Throws(
        double experience)
    {
        var formulas = CreateFormulas();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateHackingLevel(experience));
    }

    [Fact]
    public void CalculateHackChance_WithEqualSkillAndMinimumSecurity_ReturnsOne()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 2,
            startingSecurity: 2);

        var chance =
            formulas.CalculateHackChance(player, server);

        Assert.Equal(1.0, chance, precision: 10);
    }

    [Fact]
    public void CalculateHackChance_HigherSecurity_ReducesChance()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 2,
            startingSecurity: 4);

        var chance =
            formulas.CalculateHackChance(player, server);

        Assert.Equal(0.5, chance, precision: 10);
    }

    [Fact]
    public void CalculateHackChance_ChanceIsClampedToOne()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(20);

        var server = CreateServer(
            requiredHackingLevel: 1,
            minimumSecurity: 1,
            startingSecurity: 1);

        var chance =
            formulas.CalculateHackChance(player, server);

        Assert.Equal(1.0, chance);
    }

    [Fact]
    public void CalculateHackPercent_ReturnsExpectedPercentage()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 2,
            startingSecurity: 4);

        var percent =
            formulas.CalculateHackPercent(player, server);

        Assert.Equal(
            balance.HackMoneyFraction * 0.5,
            percent,
            precision: 10);
    }

    [Fact]
    public void CalculateHackAmount_ReturnsPercentageOfCurrentMoney()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            startingMoney: 50_000,
            requiredHackingLevel: 10,
            minimumSecurity: 1,
            startingSecurity: 1);

        var amount =
            formulas.CalculateHackAmount(player, server);

        var expected =
            50_000 * balance.HackMoneyFraction;

        Assert.Equal(expected, amount, precision: 10);
    }

    [Fact]
    public void CalculateHackAmount_ThreadsScaleHackAmount()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            startingMoney: 50_000,
            requiredHackingLevel: 10,
            minimumSecurity: 1,
            startingSecurity: 1);

        var oneThread =
            formulas.CalculateHackAmount(
                player,
                server,
                threads: 1);

        var tenThreads =
            formulas.CalculateHackAmount(
                player,
                server,
                threads: 10);

        Assert.Equal(
            oneThread * 10,
            tenThreads,
            precision: 10);
    }

    [Fact]
    public void CalculateHackAmount_CannotExceedCurrentMoney()
    {
        var config =
            balance with
            {
                HackMoneyFraction = 0.5
            };

        var formulas = new HackingFormulas(config);

        var player = CreatePlayerWithLevel(100);

        var server = CreateServer(
            startingMoney: 50_000,
            requiredHackingLevel: 1,
            minimumSecurity: 1,
            startingSecurity: 1);

        var amount =
            formulas.CalculateHackAmount(
                player,
                server,
                threads: 10);

        Assert.Equal(server.Money, amount);
    }

    [Fact]
    public void CalculateHackDuration_AtEqualSkillAndMinimumSecurity_ReturnsBaseDuration()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 2,
            startingSecurity: 2);

        var duration =
            formulas.CalculateHackDuration(
                player,
                server);

        Assert.Equal(
            balance.HackBaseDuration,
            duration);
    }

    [Fact]
    public void CalculateHackDuration_HigherSecurity_IncreasesDuration()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 2,
            startingSecurity: 4);

        var duration =
            formulas.CalculateHackDuration(
                player,
                server);

        Assert.Equal(
            balance.HackBaseDuration * 2,
            duration);
    }

    [Fact]
    public void CalculateHackDuration_LowerSkill_IncreasesDuration()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(5);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 1,
            startingSecurity: 1);

        var duration =
            formulas.CalculateHackDuration(
                player,
                server);

        Assert.Equal(
            balance.HackBaseDuration * 2,
            duration);
    }

    [Fact]
    public void CalculateGrowDuration_UsesConfiguredMultiplier()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 1,
            startingSecurity: 1);

        var hackDuration =
            formulas.CalculateHackDuration(player, server);

        var growDuration =
            formulas.CalculateGrowDuration(player, server);

        Assert.Equal(
            hackDuration * balance.GrowDurationMultiplier,
            growDuration);
    }

    [Fact]
    public void CalculateWeakenDuration_UsesConfiguredMultiplier()
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            requiredHackingLevel: 10,
            minimumSecurity: 1,
            startingSecurity: 1);

        var hackDuration =
            formulas.CalculateHackDuration(player, server);

        var weakenDuration =
            formulas.CalculateWeakenDuration(player, server);

        Assert.Equal(
            hackDuration * balance.WeakenDurationMultiplier,
            weakenDuration);
    }

    [Fact]
    public void CalculateGrowthFactor_WithMoreThreads_IncreasesGrowth()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            minimumSecurity: 1,
            startingSecurity: 1,
            growthParameter: 20);

        var oneThread =
            formulas.CalculateGrowthFactor(
                server,
                threads: 1);

        var tenThreads =
            formulas.CalculateGrowthFactor(
                server,
                threads: 10);

        Assert.True(oneThread > 1);
        Assert.True(tenThreads > oneThread);
    }

    [Fact]
    public void CalculateGrowthAmount_CannotExceedMaximumMoney()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            maximumMoney: 100_000,
            startingMoney: 99_000,
            minimumSecurity: 1,
            startingSecurity: 1,
            growthParameter: 100);

        var amount =
            formulas.CalculateGrowthAmount(
                server,
                threads: 100);

        Assert.Equal(1_000, amount, precision: 10);
    }

    [Fact]
    public void CalculateGrowthAmount_WhenAlreadyAtMaximum_ReturnsZero()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            maximumMoney: 100_000,
            startingMoney: 100_000);

        var amount =
            formulas.CalculateGrowthAmount(server);

        Assert.Equal(0, amount);
    }

    [Fact]
    public void CalculateGrowthAmount_WhenMoneyIsZero_ReturnsZero()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            startingMoney: 0);

        var amount =
            formulas.CalculateGrowthAmount(server);

        Assert.Equal(0, amount);
    }

    [Fact]
    public void CalculateHackSecurityIncrease_ScalesWithThreads()
    {
        var formulas = CreateFormulas();

        var increase =
            formulas.CalculateHackSecurityIncrease(
                threads: 10);

        Assert.Equal(
            balance.HackSecurityIncrease * 10,
            increase,
            precision: 10);
    }

    [Fact]
    public void CalculateGrowSecurityIncrease_ScalesWithThreads()
    {
        var formulas = CreateFormulas();

        var increase =
            formulas.CalculateGrowSecurityIncrease(
                threads: 10);

        Assert.Equal(
            balance.GrowSecurityIncrease * 10,
            increase,
            precision: 10);
    }

    [Fact]
    public void CalculateWeakenSecurityDecrease_ScalesWithThreads()
    {
        var formulas = CreateFormulas();

        var decrease =
            formulas.CalculateWeakenSecurityDecrease(
                threads: 10);

        Assert.Equal(
            balance.WeakenSecurityDecrease * 10,
            decrease,
            precision: 10);
    }

    [Fact]
    public void CalculateHackingExperience_ScalesWithDifficultyAndThreads()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            requiredHackingLevel: 10);

        var experience =
            formulas.CalculateHackingExperience(
                server,
                threads: 5);

        Assert.Equal(
            balance.BaseHackingExperience * 10 * 5,
            experience,
            precision: 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FormulaRequiringThreads_WithInvalidThreadCount_Throws(
        int threads)
    {
        var formulas = CreateFormulas();

        var player = CreatePlayerWithLevel(10);
        var server = CreateServer();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateHackAmount(
                player,
                server,
                threads));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateGrowthFactor(
                server,
                threads));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateGrowthAmount(
                server,
                threads));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateHackSecurityIncrease(
                threads));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateGrowSecurityIncrease(
                threads));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateWeakenSecurityDecrease(
                threads));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateHackingExperience(
                server,
                threads));
    }

    private HackingFormulas CreateFormulas()
    {
        return new HackingFormulas(balance);
    }

    private Player CreatePlayerWithLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        var experience =
            Math.Pow(level - 1, 2) *
            balance.HackingLevelExperienceFactor;

        return new Player(
            homeServerId: "home",
            startingHackingExperience: experience);
    }

    private static Server CreateServer(
        double maximumMoney = 100_000,
        double startingMoney = 50_000,
        double minimumSecurity = 1,
        double startingSecurity = 1,
        int requiredHackingLevel = 10,
        double growthParameter = 20,
        double maximumRam = 8)
    {
        return new Server(
            id: "n00dles",
            hostname: "n00dles",
            maximumMoney: maximumMoney,
            startingMoney: startingMoney,
            minimumSecurity: minimumSecurity,
            startingSecurity: startingSecurity,
            requiredHackingLevel: requiredHackingLevel,
            growthParameter: growthParameter,
            maximumRam: maximumRam);
    }
}