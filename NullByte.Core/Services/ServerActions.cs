using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Time;

namespace NullByte.Core.Services;

/// <summary>
/// Executes asynchronous hack, grow, and weaken operations
/// against servers in the game network.
/// </summary>
/// <remarks>
/// Server actions are intended to be executed on the game thread.
/// Their delays are measured in game time by <see cref="GameScheduler"/>.
/// </remarks>
public sealed class ServerActions
{
    private readonly GameState gameState;
    private readonly GameScheduler scheduler;
    private readonly HackingFormulas formulas;
    private readonly IRandomSource randomSource;

    public ServerActions(
        GameState gameState,
        GameScheduler scheduler,
        HackingFormulas formulas,
        IRandomSource randomSource)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));
        this.scheduler = scheduler
            ?? throw new ArgumentNullException(nameof(scheduler));
        this.formulas = formulas
            ?? throw new ArgumentNullException(nameof(formulas));
        this.randomSource = randomSource
            ?? throw new ArgumentNullException(nameof(randomSource));
    }

    /// <summary>
    /// Attempts to steal money from a server.
    /// </summary>
    public async Task<HackResult> HackAsync(
        string hostname,
        int threads = 1,
        CancellationToken cancellationToken = default)
    {
        ValidateThreads(threads);

        var server = ResolveTarget(hostname);

        if (!server.HasRootAccess)
            return HackResult.Failed("Hack failed: root access required.");

        var hackingLevel = formulas.CalculateHackingLevel(gameState.Player.HackingExperience);
        if (hackingLevel < server.RequiredHackingLevel)
            return HackResult.Failed("Hack failed: hacking level too low.");
        
        var player = gameState.Player;
        var duration = formulas.CalculateHackDuration(player, server);
        await scheduler.DelayAsync(duration, cancellationToken);

        var hackChance = formulas.CalculateHackChance(player, server);
        var succeeded = randomSource.NextDouble() < hackChance;
        var moneyStolen = 0.0;

        if (succeeded)
        {
            var requestedAmount = formulas.CalculateHackAmount(player, server, threads);
            moneyStolen = server.RemoveMoney(requestedAmount);
            player.Money += moneyStolen;
        }

        var securityIncrease = formulas.CalculateHackSecurityIncrease(threads);
        server.IncreaseSecurity(securityIncrease);

        var experienceGained = formulas.CalculateHackingExperience(server,threads);
        player.AddHackingExperience(experienceGained);

        return new HackResult(
            Succeeded: succeeded,
            FailureMessage: succeeded ? string.Empty : "Hack failed. No money was stolen.",
            MoneyStolen: moneyStolen,
            ExperienceGained: experienceGained,
            SecurityIncrease: securityIncrease,
            Duration: duration);
    }

    /// <summary>
    /// Increases a server's available money.
    /// </summary>
    public async Task<GrowResult> GrowAsync(
        string hostname,
        int threads = 1,
        CancellationToken cancellationToken = default)
    {
        ValidateThreads(threads);

        var server = ResolveTarget(hostname);

        if (!server.HasRootAccess)
            return GrowResult.Failed("Grow failed: root access required.");

        var player = gameState.Player;
        var duration = formulas.CalculateGrowDuration(player, server);

        await scheduler.DelayAsync(duration, cancellationToken);

        var requestedAmount = formulas.CalculateGrowthAmount(server, threads);
        var moneyGrown = server.AddMoney(requestedAmount);
        var securityIncrease = formulas.CalculateGrowSecurityIncrease(threads);
        server.IncreaseSecurity(securityIncrease);

        var experienceGained = formulas.CalculateHackingExperience(server, threads);
        player.AddHackingExperience(experienceGained);

        return new GrowResult(
            Succeeded: true,
            FailureMessage: string.Empty,
            MoneyGrown: moneyGrown,
            ExperienceGained: experienceGained,
            SecurityIncrease: securityIncrease,
            Duration: duration);
    }

    /// <summary>
    /// Reduces a server's security level.
    /// </summary>
    public async Task<WeakenResult> WeakenAsync(
        string hostname,
        int threads = 1,
        CancellationToken cancellationToken = default)
    {
        ValidateThreads(threads);

        var server = ResolveTarget(hostname);

        if (!server.HasRootAccess)
            return WeakenResult.Failed("Weaken failed: root access required.");

        var player = gameState.Player;
        var duration = formulas.CalculateWeakenDuration(player, server);

        await scheduler.DelayAsync(duration, cancellationToken);

        var requestedDecrease = formulas.CalculateWeakenSecurityDecrease(threads);
        var securityDecrease = server.DecreaseSecurity(requestedDecrease);
        var experienceGained = formulas.CalculateHackingExperience(server, threads);
        player.AddHackingExperience(experienceGained);

        return new WeakenResult(
            Succeeded: true, 
            FailureMessage: string.Empty,
            SecurityDecrease: securityDecrease,
            ExperienceGained: experienceGained,
            Duration: duration);
    }

    private Server ResolveTarget(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("The target hostname cannot be empty.", nameof(hostname));

        return gameState.Network.GetServer(hostname);
    }

    private static void ValidateThreads(int threads)
    {
        if (threads < 1)
            throw new ArgumentOutOfRangeException(nameof(threads), "Thread count must be at least one.");
    }
}