using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class HackingFormulasTests
{
    private readonly BalanceConfig balance = new();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(100, 1)]
    public void CalculateHackingLevel_WithLowExperience_ReturnsAtLeastOne(
        double experience,
        int expectedLevel)
    {
        var formulas = CreateFormulas();

        var level = formulas.CalculateHackingLevel(experience);

        Assert.True(level >= expectedLevel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void CalculateHackingLevel_WithExperienceForLevel_ReturnsExpectedLevel(
        int expectedLevel)
    {
        var formulas = CreateFormulas();

        var experience = ExperienceForLevel(expectedLevel);

        var actualLevel = formulas.CalculateHackingLevel(experience);

        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CalculateHackingLevel_WithInvalidExperience_Throws(
        double experience)
    {
        var formulas = CreateFormulas();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateHackingLevel(experience));
    }

    [Fact]
    public void CalculateHackChance_FollowsBitburnerFormula()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(10);

        var server = CreateServer(
            startingSecurity: 2,
            requiredHackingLevel: 10,
            hasRootAccess: true);

        var difficultyMultiplier =
            (100.0 - server.Security) / 100.0;

        var effectiveSkill = Math.Max(
            1.0,
            balance.HackChanceSkillFactor * 10);

        var skillChance =
            (effectiveSkill - server.RequiredHackingLevel)
            / effectiveSkill;

        var expectedChance =
            difficultyMultiplier
            * skillChance
            * balance.HackingChanceMultiplier
            * balance.HackingIntelligenceBonus;

        var actualChance =
            formulas.CalculateHackChance(player, server);

        Assert.Equal(
            expectedChance,
            actualChance,
            precision: 10);
    }

    [Fact]
    public void CalculateHackChance_WithoutRootAccess_ReturnsZero()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(100);

        var server = CreateServer(
            requiredHackingLevel: 1,
            hasRootAccess: false);

        var chance =
            formulas.CalculateHackChance(player, server);

        Assert.Equal(0.0, chance);
    }

    [Fact]
    public void CalculateHackChance_WithSecurityAtOneHundred_ReturnsZero()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(100);

        var server = CreateServer(
            minimumSecurity: 1,
            baseDifficulty: 100,
            startingSecurity: 100,
            requiredHackingLevel: 1,
            hasRootAccess: true);

        var chance =
            formulas.CalculateHackChance(player, server);

        Assert.Equal(0.0, chance);
    }

    [Fact]
    public void CalculateHackChance_HigherSecurityReducesChance()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(25);

        var lowSecurityServer = CreateServer(
            startingSecurity: 5,
            requiredHackingLevel: 10,
            hasRootAccess: true);

        var highSecurityServer = CreateServer(
            baseDifficulty: 50,
            startingSecurity: 50,
            requiredHackingLevel: 10,
            hasRootAccess: true);

        var lowSecurityChance =
            formulas.CalculateHackChance(
                player,
                lowSecurityServer);

        var highSecurityChance =
            formulas.CalculateHackChance(
                player,
                highSecurityServer);

        Assert.True(
            highSecurityChance < lowSecurityChance);
    }

    [Fact]
    public void CalculateHackChance_HigherSkillIncreasesChance()
    {
        var formulas = CreateFormulas();

        var lowLevelPlayer =
            CreatePlayerWithLevel(10);

        var highLevelPlayer =
            CreatePlayerWithLevel(50);

        var server = CreateServer(
            startingSecurity: 10,
            requiredHackingLevel: 10,
            hasRootAccess: true);

        var lowLevelChance =
            formulas.CalculateHackChance(
                lowLevelPlayer,
                server);

        var highLevelChance =
            formulas.CalculateHackChance(
                highLevelPlayer,
                server);

        Assert.True(
            highLevelChance > lowLevelChance);
    }

    [Fact]
    public void CalculateHackChance_WhenSkillIsTooLow_ReturnsZero()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(1);

        var server = CreateServer(
            startingSecurity: 1,
            requiredHackingLevel: 100,
            hasRootAccess: true);

        var chance =
            formulas.CalculateHackChance(player, server);

        Assert.Equal(0.0, chance);
    }

    [Fact]
    public void CalculateHackPercent_FollowsBitburnerFormula()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(20);

        var server = CreateServer(
            startingSecurity: 5,
            requiredHackingLevel: 10);

        var difficultyMultiplier =
            (100.0 - server.Security) / 100.0;

        var skillMultiplier =
            (20.0
             - (server.RequiredHackingLevel - 1.0))
            / 20.0;

        var expectedPercent =
            difficultyMultiplier
            * skillMultiplier
            * balance.HackingMoneyMultiplier
            / balance.HackBalanceFactor;

        var actualPercent =
            formulas.CalculateHackPercent(
                player,
                server);

        Assert.Equal(
            expectedPercent,
            actualPercent,
            precision: 10);
    }

    [Fact]
    public void CalculateHackPercent_WhenSkillIsTooLow_ReturnsZero()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(1);

        var server = CreateServer(
            requiredHackingLevel: 100);

        var percent =
            formulas.CalculateHackPercent(
                player,
                server);

        Assert.Equal(0.0, percent);
    }

    [Fact]
    public void CalculateHackPercent_WithSecurityAtOneHundred_ReturnsZero()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(100);

        var server = CreateServer(
            minimumSecurity: 1,
            baseDifficulty: 100,
            startingSecurity: 100,
            requiredHackingLevel: 1);

        var percent =
            formulas.CalculateHackPercent(
                player,
                server);

        Assert.Equal(0.0, percent);
    }

    [Fact]
    public void CalculateHackPercent_HigherSecurityReducesPercent()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(50);

        var lowSecurityServer = CreateServer(
            startingSecurity: 5,
            requiredHackingLevel: 10);

        var highSecurityServer = CreateServer(
            baseDifficulty: 50,
            startingSecurity: 50,
            requiredHackingLevel: 10);

        var lowSecurityPercent =
            formulas.CalculateHackPercent(
                player,
                lowSecurityServer);

        var highSecurityPercent =
            formulas.CalculateHackPercent(
                player,
                highSecurityServer);

        Assert.True(
            highSecurityPercent < lowSecurityPercent);
    }

    [Fact]
    public void CalculateHackAmount_ReturnsCurrentMoneyTimesHackPercent()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(50);

        var server = CreateServer(
            startingMoney: 50_000,
            startingSecurity: 5,
            requiredHackingLevel: 10);

        var percent =
            formulas.CalculateHackPercent(
                player,
                server);

        var amount =
            formulas.CalculateHackAmount(
                player,
                server);

        Assert.Equal(
            server.Money * percent,
            amount,
            precision: 10);
    }

    [Fact]
    public void CalculateHackAmount_MultipliesPercentByThreads()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(50);

        var server = CreateServer(
            startingMoney: 50_000,
            startingSecurity: 5,
            requiredHackingLevel: 10);

        const int threads = 10;

        var percentPerThread =
            formulas.CalculateHackPercent(
                player,
                server);

        var expectedPercent = Math.Min(
            1.0,
            percentPerThread * threads);

        var amount =
            formulas.CalculateHackAmount(
                player,
                server,
                threads);

        Assert.Equal(
            server.Money * expectedPercent,
            amount,
            precision: 10);
    }

    [Fact]
    public void CalculateHackAmount_WhenThreadPercentExceedsOne_ReturnsAllMoney()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(1_000);

        var server = CreateServer(
            startingMoney: 50_000,
            startingSecurity: 1,
            requiredHackingLevel: 1);

        var amount =
            formulas.CalculateHackAmount(
                player,
                server,
                threads: 10_000);

        Assert.Equal(
            server.Money,
            amount,
            precision: 10);
    }

    [Fact]
    public void CalculateHackDuration_FollowsBitburnerFormula()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(20);

        var server = CreateServer(
            startingSecurity: 5,
            requiredHackingLevel: 10);

        var difficulty =
            server.RequiredHackingLevel
            * server.Security;

        var skillFactor =
            (balance.HackDifficultyFactor
             * difficulty
             + balance.HackDifficultyBase)
            / (20.0 + balance.HackSkillBase);

        var expectedSeconds =
            balance.HackTimeMultiplier
            * skillFactor
            / (balance.HackingSpeedMultiplier
               * balance.HackingIntelligenceBonus);

        var duration =
            formulas.CalculateHackDuration(
                player,
                server);

        Assert.Equal(
            expectedSeconds,
            duration.TotalSeconds,
            precision: 4);
    }

    [Fact]
    public void CalculateHackDuration_HigherSkillReducesDuration()
    {
        var formulas = CreateFormulas();

        var lowLevelPlayer =
            CreatePlayerWithLevel(10);

        var highLevelPlayer =
            CreatePlayerWithLevel(100);

        var server = CreateServer(
            startingSecurity: 10,
            requiredHackingLevel: 10);

        var lowLevelDuration =
            formulas.CalculateHackDuration(
                lowLevelPlayer,
                server);

        var highLevelDuration =
            formulas.CalculateHackDuration(
                highLevelPlayer,
                server);

        Assert.True(
            highLevelDuration < lowLevelDuration);
    }

    [Fact]
    public void CalculateHackDuration_HigherSecurityIncreasesDuration()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(50);

        var lowSecurityServer = CreateServer(
            startingSecurity: 5,
            requiredHackingLevel: 10);

        var highSecurityServer = CreateServer(
            baseDifficulty: 50,
            startingSecurity: 50,
            requiredHackingLevel: 10);

        var lowSecurityDuration =
            formulas.CalculateHackDuration(
                player,
                lowSecurityServer);

        var highSecurityDuration =
            formulas.CalculateHackDuration(
                player,
                highSecurityServer);

        Assert.True(
            highSecurityDuration > lowSecurityDuration);
    }

    [Fact]
    public void CalculateGrowDuration_ReturnsHackDurationTimesGrowMultiplier()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(20);
        var server = CreateServer();

        var hackDuration =
            formulas.CalculateHackDuration(
                player,
                server);

        var growDuration =
            formulas.CalculateGrowDuration(
                player,
                server);

        Assert.Equal(
            hackDuration.TotalSeconds
            * balance.GrowDurationMultiplier,
            growDuration.TotalSeconds,
            precision: 10);
    }

    [Fact]
    public void CalculateWeakenDuration_ReturnsHackDurationTimesWeakenMultiplier()
    {
        var formulas = CreateFormulas();
        var player = CreatePlayerWithLevel(20);
        var server = CreateServer();

        var hackDuration =
            formulas.CalculateHackDuration(
                player,
                server);

        var weakenDuration =
            formulas.CalculateWeakenDuration(
                player,
                server);

        Assert.Equal(
            hackDuration.TotalSeconds
            * balance.WeakenDurationMultiplier,
            weakenDuration.TotalSeconds,
            precision: 10);
    }

    [Fact]
    public void CalculateGrowthFactor_FollowsBitburnerFormula()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            startingSecurity: 5,
            growthParameter: 30);

        const int threads = 10;
        const int cores = 1;

        var adjustedGrowthLog = Math.Log(
            1.0
            + balance.ServerBaseGrowthIncrement
            / server.Security);

        var maximumGrowthLog =
            Math.Log(balance.ServerMaximumGrowthRate);

        adjustedGrowthLog = Math.Min(
            adjustedGrowthLog,
            maximumGrowthLog);

        var growthPercentage =
            server.GrowthParameter / 100.0;

        var coreBonus =
            1.0 + (cores - 1.0) / 16.0;

        var expectedGrowthLog =
            adjustedGrowthLog
            * growthPercentage
            * balance.ServerGrowthMultiplier
            * coreBonus
            * threads;

        var expectedFactor =
            Math.Exp(expectedGrowthLog);

        var actualFactor =
            formulas.CalculateGrowthFactor(
                server,
                threads,
                cores);

        Assert.Equal(
            expectedFactor,
            actualFactor,
            precision: 10);
    }

    [Fact]
    public void CalculateGrowthFactor_WithZeroGrowthParameter_ReturnsOne()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            growthParameter: 0);

        var factor =
            formulas.CalculateGrowthFactor(
                server,
                threads: 10);

        Assert.Equal(1.0, factor);
    }

    [Fact]
    public void CalculateGrowthFactor_MoreThreadsIncreaseGrowth()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            startingSecurity: 5,
            growthParameter: 30);

        var oneThreadFactor =
            formulas.CalculateGrowthFactor(
                server,
                threads: 1);

        var tenThreadFactor =
            formulas.CalculateGrowthFactor(
                server,
                threads: 10);

        Assert.True(
            tenThreadFactor > oneThreadFactor);
    }

    [Fact]
    public void CalculateGrowthFactor_MoreCoresIncreaseGrowth()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            startingSecurity: 5,
            growthParameter: 30);

        var oneCoreFactor =
            formulas.CalculateGrowthFactor(
                server,
                threads: 10,
                cores: 1);

        var eightCoreFactor =
            formulas.CalculateGrowthFactor(
                server,
                threads: 10,
                cores: 8);

        Assert.True(
            eightCoreFactor > oneCoreFactor);
    }

    [Fact]
    public void CalculateGrowthFactor_HigherSecurityReducesGrowth()
    {
        var formulas = CreateFormulas();

        var lowSecurityServer = CreateServer(
            startingSecurity: 5,
            growthParameter: 30);

        var highSecurityServer = CreateServer(
            baseDifficulty: 50,
            startingSecurity: 50,
            growthParameter: 30);

        var lowSecurityFactor =
            formulas.CalculateGrowthFactor(
                lowSecurityServer,
                threads: 10);

        var highSecurityFactor =
            formulas.CalculateGrowthFactor(
                highSecurityServer,
                threads: 10);

        Assert.True(
            highSecurityFactor < lowSecurityFactor);
    }

    [Fact]
    public void CalculateGrowthAmount_FollowsBitburnerFormula()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            maximumMoney: 100_000,
            startingMoney: 20_000,
            startingSecurity: 5,
            growthParameter: 30);

        const int threads = 10;
        const int cores = 1;

        var growthFactor =
            formulas.CalculateGrowthFactor(
                server,
                threads,
                cores);

        var expectedTargetMoney = Math.Min(
            (server.Money + threads) * growthFactor,
            server.MaximumMoney);

        var expectedAmount =
            expectedTargetMoney - server.Money;

        var actualAmount =
            formulas.CalculateGrowthAmount(
                server,
                threads,
                cores);

        Assert.Equal(
            expectedAmount,
            actualAmount,
            precision: 10);
    }

    [Fact]
    public void CalculateGrowthAmount_WhenMoneyIsZero_CanRecover()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            maximumMoney: 100_000,
            startingMoney: 0,
            startingSecurity: 5,
            growthParameter: 30);

        var amount =
            formulas.CalculateGrowthAmount(
                server,
                threads: 10);

        Assert.True(amount > 10);
        Assert.True(amount <= server.MaximumMoney);
    }

    [Fact]
    public void CalculateGrowthAmount_DoesNotExceedMaximumMoney()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            maximumMoney: 100_000,
            startingMoney: 99_999,
            startingSecurity: 1,
            growthParameter: 100);

        var amount =
            formulas.CalculateGrowthAmount(
                server,
                threads: 10_000,
                cores: 16);

        Assert.Equal(
            1.0,
            amount,
            precision: 10);
    }

    [Fact]
    public void CalculateGrowthAmount_WhenMoneyIsAlreadyMaximum_ReturnsZero()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            maximumMoney: 100_000,
            startingMoney: 100_000);

        var amount =
            formulas.CalculateGrowthAmount(
                server,
                threads: 10);

        Assert.Equal(0.0, amount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void CalculateHackSecurityIncrease_ScalesWithThreads(
        int threads)
    {
        var formulas = CreateFormulas();

        var increase =
            formulas.CalculateHackSecurityIncrease(
                threads);

        Assert.Equal(
            balance.HackSecurityIncrease * threads,
            increase,
            precision: 10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void CalculateGrowSecurityIncrease_ScalesWithThreads(
        int threads)
    {
        var formulas = CreateFormulas();

        var increase =
            formulas.CalculateGrowSecurityIncrease(
                threads);

        Assert.Equal(
            balance.GrowSecurityIncrease * threads,
            increase,
            precision: 10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void CalculateWeakenSecurityDecrease_ScalesWithThreads(
        int threads)
    {
        var formulas = CreateFormulas();

        var decrease =
            formulas.CalculateWeakenSecurityDecrease(
                threads);

        Assert.Equal(
            balance.WeakenSecurityDecrease * threads,
            decrease,
            precision: 10);
    }

    [Fact]
    public void CalculateHackingExperience_UsesBaseDifficulty()
    {
        var formulas = CreateFormulas();

        var server = CreateServer(
            minimumSecurity: 1,
            baseDifficulty: 20,
            startingSecurity: 80,
            requiredHackingLevel: 100);

        const int threads = 5;

        var expectedPerThread =
            balance.BaseHackingExperience
            + server.BaseDifficulty
            * balance.HackingExperienceDifficultyFactor;

        var expectedExperience =
            expectedPerThread
            * balance.HackingExperienceMultiplier
            * threads;

        var actualExperience =
            formulas.CalculateHackingExperience(
                server,
                threads);

        Assert.Equal(
            expectedExperience,
            actualExperience,
            precision: 10);
    }

    [Fact]
    public void CalculateHackingExperience_DoesNotUseCurrentSecurity()
    {
        var formulas = CreateFormulas();

        var lowSecurityServer = CreateServer(
            minimumSecurity: 1,
            baseDifficulty: 20,
            startingSecurity: 20);

        var highSecurityServer = CreateServer(
            minimumSecurity: 1,
            baseDifficulty: 20,
            startingSecurity: 80);

        var lowSecurityExperience =
            formulas.CalculateHackingExperience(
                lowSecurityServer);

        var highSecurityExperience =
            formulas.CalculateHackingExperience(
                highSecurityServer);

        Assert.Equal(
            lowSecurityExperience,
            highSecurityExperience,
            precision: 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThreadBasedCalculations_WithInvalidThreadCount_Throw(
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GrowthCalculations_WithInvalidCoreCount_Throw(
        int cores)
    {
        var formulas = CreateFormulas();
        var server = CreateServer();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateGrowthFactor(
                server,
                threads: 1,
                cores: cores));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => formulas.CalculateGrowthAmount(
                server,
                threads: 1,
                cores: cores));
    }

    private HackingFormulas CreateFormulas()
    {
        return new HackingFormulas(balance);
    }

    private Player CreatePlayerWithLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            level,
            1);

        return new Player(
            homeHostname: "home",
            startingHackingExperience:
                ExperienceForLevel(level));
    }

    private double ExperienceForLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            level,
            1);

        if (level == 1)
            return 0.0;

        /*
         * Invert:
         *
         * level = floor(
         *     HackingSkillMultiplier
         *     * (
         *         SkillLogarithmMultiplier
         *         * log(experience + SkillExperienceOffset)
         *         - SkillLevelOffset
         *     ))
         */
        var baseSkillRequired =
            level
            / balance.HackingSkillMultiplier;

        var experience =
            Math.Exp(
                (baseSkillRequired
                 + balance.SkillLevelOffset)
                / balance.SkillLogarithmMultiplier)
            - balance.SkillExperienceOffset;

        experience = Math.Max(0.0, experience);

        /*
         * The inverse calculation can land immediately below the
         * requested integer because of floating-point rounding.
         * Move upward until the production formula reaches the
         * requested level.
         */
        var formulas = CreateFormulas();

        while (formulas.CalculateHackingLevel(experience) < level)
        {
            var increment = Math.Max(
                1e-9,
                Math.Abs(experience) * 1e-12);

            experience += increment;
        }

        /*
         * Verify that the helper did not overshoot. With the
         * configured logarithmic curve, the small increment above
         * cannot normally skip a level, but this assertion makes a
         * future balance change fail clearly.
         */
        var calculatedLevel =
            formulas.CalculateHackingLevel(experience);

        if (calculatedLevel != level)
        {
            throw new InvalidOperationException(
                $"Could not create experience for hacking level " +
                $"{level}. Calculated level was {calculatedLevel}.");
        }

        return experience;
    }

    private static Server CreateServer(
        double maximumMoney = 100_000,
        double startingMoney = 50_000,
        double minimumSecurity = 1,
        double baseDifficulty = 1,
        double startingSecurity = 1,
        int requiredHackingLevel = 10,
        double growthParameter = 20,
        double maximumRam = 8,
        bool hasRootAccess = true)
    {
        return new Server(
            hostname: "n00dles",
            maximumMoney: maximumMoney,
            startingMoney: startingMoney,
            minimumSecurity: minimumSecurity,
            baseDifficulty: baseDifficulty,
            startingSecurity: startingSecurity,
            requiredHackingLevel: requiredHackingLevel,
            growthParameter: growthParameter,
            maximumRam: maximumRam,
            hasRootAccess: hasRootAccess);
    }
}