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
        {
            throw new ArgumentOutOfRangeException(
                nameof(experience),
                "Experience must be finite and non-negative.");
        }

        var level =
            Math.Sqrt(experience / balance.HackingLevelExperienceFactor)
            + 1.0;

        return Math.Max(1, (int)Math.Floor(level));
    }

    /// <summary>
    /// Calculates the probability that a single hacking attempt
    /// succeeds.
    /// </summary>
    public double CalculateHackChance(
        Player player,
        Server server)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(server);

        var hackingLevel =
            CalculateHackingLevel(player.HackingExperience);

        var skillFactor =
            (double)hackingLevel / server.RequiredHackingLevel;

        var securityFactor =
            server.MinimumSecurity / server.Security;

        return Math.Clamp(
            skillFactor * securityFactor,
            0.0,
            1.0);
    }

    /// <summary>
    /// Calculates the fraction of the server's current money stolen
    /// by a single successful hacking thread.
    /// </summary>
    public double CalculateHackPercent(
        Player player,
        Server server)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(server);

        var hackingLevel =
            CalculateHackingLevel(player.HackingExperience);

        var skillFactor =
            (double)hackingLevel / server.RequiredHackingLevel;

        var securityFactor =
            server.MinimumSecurity / server.Security;

        return Math.Clamp(
            balance.HackMoneyFraction *
            skillFactor *
            securityFactor,
            0.0,
            1.0);
    }

    /// <summary>
    /// Calculates the amount of money stolen by a successful hack.
    /// </summary>
    public double CalculateHackAmount(
        Player player,
        Server server,
        int threads = 1)
    {
        ValidateThreads(threads);

        var percentPerThread =
            CalculateHackPercent(player, server);

        var totalPercent =
            Math.Min(1.0, percentPerThread * threads);

        return server.Money * totalPercent;
    }

    /// <summary>
    /// Calculates the duration of a hacking operation.
    /// </summary>
    public TimeSpan CalculateHackDuration(
        Player player,
        Server server)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(server);

        var hackingLevel =
            CalculateHackingLevel(player.HackingExperience);

        var skillFactor =
            Math.Max(
                1.0,
                (double)server.RequiredHackingLevel / hackingLevel);

        var securityFactor =
            server.Security / server.MinimumSecurity;

        var multiplier =
            skillFactor * securityFactor;

        return MultiplyDuration(
            balance.HackBaseDuration,
            multiplier);
    }

    /// <summary>
    /// Calculates the duration of a grow operation.
    /// </summary>
    public TimeSpan CalculateGrowDuration(
        Player player,
        Server server)
    {
        return MultiplyDuration(
            CalculateHackDuration(player, server),
            balance.GrowDurationMultiplier);
    }

    /// <summary>
    /// Calculates the duration of a weaken operation.
    /// </summary>
    public TimeSpan CalculateWeakenDuration(
        Player player,
        Server server)
    {
        return MultiplyDuration(
            CalculateHackDuration(player, server),
            balance.WeakenDurationMultiplier);
    }

    /// <summary>
    /// Calculates the amount by which a grow operation multiplies
    /// the server's current money.
    /// </summary>
    public double CalculateGrowthFactor(
        Server server,
        int threads = 1)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateThreads(threads);

        var securityFactor =
            server.MinimumSecurity / server.Security;

        var growthPerThread =
            1.0 +
            (balance.ServerBaseGrowthRate - 1.0) *
            server.GrowthParameter *
            securityFactor;

        return Math.Pow(growthPerThread, threads);
    }

    /// <summary>
    /// Calculates how much money should be added by a grow
    /// operation without exceeding the server's maximum money.
    /// </summary>
    public double CalculateGrowthAmount(
        Server server,
        int threads = 1)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateThreads(threads);

        if (server.Money <= 0 ||
            server.Money >= server.MaximumMoney)
        {
            return 0;
        }

        var growthFactor =
            CalculateGrowthFactor(server, threads);

        var targetMoney =
            server.Money * growthFactor;

        var clampedTarget =
            Math.Min(targetMoney, server.MaximumMoney);

        return clampedTarget - server.Money;
    }

    /// <summary>
    /// Calculates the security increase caused by hacking.
    /// </summary>
    public double CalculateHackSecurityIncrease(
        int threads = 1)
    {
        ValidateThreads(threads);

        return balance.HackSecurityIncrease * threads;
    }

    /// <summary>
    /// Calculates the security increase caused by growing.
    /// </summary>
    public double CalculateGrowSecurityIncrease(
        int threads = 1)
    {
        ValidateThreads(threads);

        return balance.GrowSecurityIncrease * threads;
    }

    /// <summary>
    /// Calculates the maximum security reduction caused by
    /// weakening.
    /// </summary>
    public double CalculateWeakenSecurityDecrease(
        int threads = 1)
    {
        ValidateThreads(threads);

        return balance.WeakenSecurityDecrease * threads;
    }

    /// <summary>
    /// Calculates the hacking experience awarded for an operation.
    /// </summary>
    public double CalculateHackingExperience(
        Server server,
        int threads = 1)
    {
        ArgumentNullException.ThrowIfNull(server);
        ValidateThreads(threads);

        var difficultyMultiplier =
            Math.Max(1.0, server.RequiredHackingLevel);

        return balance.BaseHackingExperience *
               difficultyMultiplier *
               threads;
    }

    private static void ValidateThreads(int threads)
    {
        if (threads < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threads),
                "Thread count must be at least one.");
        }
    }

    private static TimeSpan MultiplyDuration(
        TimeSpan duration,
        double multiplier)
    {
        if (!double.IsFinite(multiplier) || multiplier < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                "Duration multiplier must be finite and non-negative.");
        }

        return TimeSpan.FromTicks(
            checked((long)(duration.Ticks * multiplier)));
    }
}