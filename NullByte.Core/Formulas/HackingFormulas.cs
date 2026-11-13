using NullByte.Core.Configuration;
using NullByte.Core.Domain;

namespace NullByte.Core.Formulas;

/// <summary>
/// Calculates hacking-related game mechanics.
///
/// This class contains no mutable game state and does not modify
/// players or servers.
/// </summary>
public sealed class HackingFormulas
{
    private readonly BalanceConfig balance;

    public HackingFormulas(BalanceConfig balance)
    {
        this.balance = balance
            ?? throw new ArgumentNullException(nameof(balance));
    }

    /// <summary>
    /// Calculates the player's hacking level from accumulated
    /// hacking experience.
    /// </summary>
    public int CalculateHackingLevel(double experience)
    {
        if (!double.IsFinite(experience) || experience < 0)
            throw new ArgumentOutOfRangeException(nameof(experience), "Experience must be finite and non-negative.");

        var baseSkill =
            balance.SkillLogarithmMultiplier
            * Math.Log(experience + balance.SkillExperienceOffset)
            - balance.SkillLevelOffset;

        var modifiedSkill =
            baseSkill
            * balance.HackingSkillMultiplier;

        return Math.Max(1, (int)Math.Floor(modifiedSkill));
    }

    public double CalculateExperienceForHackingLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        if (!double.IsFinite(balance.HackingSkillMultiplier) || balance.HackingSkillMultiplier <= 0)
            throw new InvalidOperationException("Hacking skill multiplier must be finite and positive.");

        var requiredBaseSkill = level / balance.HackingSkillMultiplier;

        var experience =
            Math.Exp((requiredBaseSkill + balance.SkillLevelOffset)
                / balance.SkillLogarithmMultiplier)
            - balance.SkillExperienceOffset;

        experience = Math.Max(0.0, experience);

        while (CalculateHackingLevel(experience) < level)
        {
            var next = Math.BitIncrement(experience);

            if (!double.IsFinite(next) || next <= experience)
                throw new OverflowException($"Could not calculate finite experience for level {level}.");

            experience = next;
        }

        return experience;
    }

    /// <summary>
    /// Calculates the probability that a single hacking attempt
    /// succeeds.
    /// </summary>
    public double CalculateHackChance(Player player, Server server)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(server);

        if (!server.HasRootAccess || server.Security >= 100.0)
            return 0.0;

        var hackingLevel = CalculateHackingLevel(player.HackingExperience);

        var difficultyMultiplier = (100.0 - server.Security) / 100.0;

        var effectiveSkill = Math.Max(1.0, balance.HackChanceSkillFactor * hackingLevel);

        var skillChance =
            (effectiveSkill
             - server.RequiredHackingLevel)
            / effectiveSkill;

        var chance =
            skillChance
            * difficultyMultiplier
            * balance.HackingChanceMultiplier
            * balance.HackingIntelligenceBonus;

        return Math.Clamp(chance, 0.0, 1.0);
    }

    /// <summary>
    /// Calculates the fraction of the server's current money stolen
    /// by a single successful hacking thread.
    /// </summary>
    public double CalculateHackPercent(Player player, Server server)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(server);

        if (server.Security >= 100.0)
            return 0.0;

        var hackingLevel = CalculateHackingLevel(player.HackingExperience);

        var difficultyMultiplier = (100.0 - server.Security) / 100.0;

        var skillMultiplier = (hackingLevel - (server.RequiredHackingLevel - 1.0)) / hackingLevel;

        var percent =
            difficultyMultiplier
            * skillMultiplier
            * balance.HackingMoneyMultiplier
            / balance.HackBalanceFactor;

        return Math.Clamp(percent, 0.0, 1.0);
    }

    /// <summary>
    /// Calculates the amount of money stolen by a successful hack.
    /// </summary>
    public double CalculateHackAmount(Player player, Server server, int threads = 1)
    {
        ValidateThreads(threads);

        var percentPerThread = CalculateHackPercent(player, server);

        var totalPercent = Math.Min(1.0, percentPerThread * threads);

        return server.Money * totalPercent;
    }

    /// <summary>
    /// Calculates the duration of a hacking operation.
    /// </summary>
    public TimeSpan CalculateHackDuration(Player player, Server server)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(server);

        var hackingLevel = CalculateHackingLevel(player.HackingExperience);

        var difficulty = server.RequiredHackingLevel * server.Security;

        var skillFactor =
            (balance.HackDifficultyFactor * difficulty + balance.HackDifficultyBase)
            / (hackingLevel + balance.HackSkillBase);

        var seconds =
            balance.HackTimeMultiplier * skillFactor
            / (balance.HackingSpeedMultiplier * balance.HackingIntelligenceBonus);

        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Calculates the duration of a grow operation.
    /// </summary>
    public TimeSpan CalculateGrowDuration(Player player, Server server)
    {
        return MultiplyDuration(
            CalculateHackDuration(player, server),
            balance.GrowDurationMultiplier);
    }

    /// <summary>
    /// Calculates the duration of a weaken operation.
    /// </summary>
    public TimeSpan CalculateWeakenDuration(Player player, Server server)
    {
        return MultiplyDuration(
            CalculateHackDuration(player, server),
            balance.WeakenDurationMultiplier);
    }

    /// <summary>
    /// Calculates the amount by which a grow operation multiplies
    /// server money.
    /// </summary>
    public double CalculateGrowthFactor(Server server, int threads = 1, int cores = 1)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateThreads(threads);
        ValidateCores(cores);

        if (server.GrowthParameter <= 0)
            return 1.0;

        var adjustedGrowthLog = Math.Log(1.0 + balance.ServerBaseGrowthIncrement / server.Security);

        var maximumGrowthLog =Math.Log(balance.ServerMaximumGrowthRate);

        adjustedGrowthLog = Math.Min(adjustedGrowthLog, maximumGrowthLog);

        var growthPercentage = server.GrowthParameter / 100.0;

        var coreBonus = 1.0 + (cores - 1.0) / 16.0;

        var growthLog =
            adjustedGrowthLog
            * growthPercentage
            * balance.ServerGrowthMultiplier
            * coreBonus
            * threads;

        return Math.Exp(growthLog);
    }

    /// <summary>
    /// Calculates how much money should be added by a grow
    /// operation without exceeding the server's maximum money.
    /// </summary>
    public double CalculateGrowthAmount(Server server, int threads = 1, int cores = 1)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateThreads(threads);
        ValidateCores(cores);

        if (server.Money >= server.MaximumMoney)
            return 0.0;

        var growthFactor =
            CalculateGrowthFactor(
                server,
                threads,
                cores);

        // Bitburner adds one monetary unit per growth thread before
        // applying multiplicative growth. This lets an empty server
        // recover from zero money.
        var targetMoney = (server.Money + threads)* growthFactor;

        var clampedTarget = Math.Min(targetMoney, server.MaximumMoney);

        return Math.Max(0.0, clampedTarget - server.Money);
    }

    /// <summary>
    /// Calculates the security increase caused by hacking.
    /// </summary>
    public double CalculateHackSecurityIncrease(int threads = 1)
    {
        ValidateThreads(threads);

        return balance.HackSecurityIncrease * threads;
    }

    /// <summary>
    /// Calculates the security increase caused by growing.
    /// </summary>
    public double CalculateGrowSecurityIncrease(int threads = 1)
    {
        ValidateThreads(threads);

        return balance.GrowSecurityIncrease * threads;
    }

    /// <summary>
    /// Calculates the maximum security reduction caused by
    /// weakening.
    /// </summary>
    public double CalculateWeakenSecurityDecrease(int threads = 1, int cores = 1)
    {
        ValidateThreads(threads);
        ValidateCores(cores);
        
        var coreBonus = 1.0 + (cores - 1.0) / 16.0;

        return balance.WeakenSecurityDecrease * threads * coreBonus;
    }

    /// <summary>
    /// Calculates the hacking experience awarded for an operation.
    /// </summary>
    public double CalculateHackingExperience(Server server,int threads = 1)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateThreads(threads);
        
        if (server.BaseDifficulty <= 0)
            return 0.0;

        var experiencePerThread =
            balance.BaseHackingExperience
            + server.BaseDifficulty
            * balance.HackingExperienceDifficultyFactor;

        return experiencePerThread
               * balance.HackingExperienceMultiplier
               * threads;
    }

    public double GetFailedHackExperienceFactor() => balance.FailedHackExperienceFactor;

    private static void ValidateThreads(int threads)
    {
        if (threads < 1)
            throw new ArgumentOutOfRangeException(nameof(threads), "Thread count must be at least one.");
    }

    private static void ValidateCores(int cores)
    {
        if (cores < 1)
            throw new ArgumentOutOfRangeException(nameof(cores), "Core count must be at least one.");
    }

    private static TimeSpan MultiplyDuration(TimeSpan duration, double multiplier)
    {
        if (!double.IsFinite(multiplier) || multiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Duration multiplier must be finite and non-negative.");

        return TimeSpan.FromTicks(checked((long)(duration.Ticks * multiplier)));
    }
}