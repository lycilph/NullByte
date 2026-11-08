namespace NullByte.Core.Configuration;

/// <summary>
/// Contains tunable values that control game balance and
/// simulation behavior.
/// </summary>
public sealed record BalanceConfig
{
    // Simulation

    /// <summary>
    /// Controls how quickly game time advances relative to real time.
    /// A value of 1.0 means one second of real time equals one second
    /// of game time.
    /// </summary>
    public double GameSpeedMultiplier { get; init; } = 1.0;

    // Hacking

    /// <summary>
    /// Base fraction of a server's available money that can be
    /// stolen by one hacking thread before skill and security
    /// modifiers are applied.
    /// </summary>
    public double HackMoneyFraction { get; init; } = 0.002;

    /// <summary>
    /// Security added by each hacking thread when a hack completes.
    /// </summary>
    public double HackSecurityIncrease { get; init; } = 0.002;

    /// <summary>
    /// Security added by each growing thread when a grow completes.
    /// </summary>
    public double GrowSecurityIncrease { get; init; } = 0.004;

    /// <summary>
    /// Security removed by each weakening thread when a weaken
    /// completes.
    /// </summary>
    public double WeakenSecurityDecrease { get; init; } = 0.05;

    // Action durations

    /// <summary>
    /// Base duration used when calculating hacking time.
    /// </summary>
    public TimeSpan HackBaseDuration { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Grow duration relative to the calculated hacking duration.
    /// </summary>
    public double GrowDurationMultiplier { get; init; } = 3.2;

    /// <summary>
    /// Weaken duration relative to the calculated hacking duration.
    /// </summary>
    public double WeakenDurationMultiplier { get; init; } = 4.0;

    // Experience and progression

    /// <summary>
    /// Base hacking experience awarded by server actions before
    /// server-specific modifiers are applied.
    /// </summary>
    public double BaseHackingExperience { get; init; } = 3.0;

    /// <summary>
    /// Controls how quickly hacking level increases with experience.
    /// </summary>
    public double HackingLevelExperienceFactor { get; init; } = 32.0;

    // Growth

    /// <summary>
    /// Base growth rate used by the server growth formula.
    /// </summary>
    public double ServerBaseGrowthRate { get; init; } = 1.03;

    // Botnet

    /// <summary>
    /// Multiplier applied to all botnet production. Use it to tune the
    /// botnet's share of the economy without touching the cost curves.
    /// </summary>
    public double BotnetProductionMultiplier { get; init; } = 1.0;

    /// <summary>
    /// Money produced per second by each level of a node, before the RAM
    /// and core bonuses.
    /// </summary>
    public double BotnetProductionPerLevel { get; init; } = 1.5;

    /// <summary>
    /// Production grows by this factor for every point of RAM above one.
    /// </summary>
    public double BotnetRamBonusBase { get; init; } = 1.035;

    /// <summary>
    /// Cost of the first node. Each further node costs
    /// <see cref="BotnetNodeCostGrowth"/> times the previous one.
    /// </summary>
    public double BotnetNodeBaseCost { get; init; } = 1_000;

    public double BotnetNodeCostGrowth { get; init; } = 1.85;

    /// <summary>
    /// Cost of raising a level-one node to level two. Each further level
    /// costs <see cref="BotnetLevelCostGrowth"/> times the previous one.
    /// </summary>
    public double BotnetLevelBaseCost { get; init; } = 500;

    public double BotnetLevelCostGrowth { get; init; } = 1.04;

    /// <summary>
    /// Cost per point of current RAM for the first RAM upgrade. Each
    /// further RAM upgrade multiplies it by <see cref="BotnetRamCostGrowth"/>.
    /// </summary>
    public double BotnetRamBaseCost { get; init; } = 30_000;

    public double BotnetRamCostGrowth { get; init; } = 1.28;

    /// <summary>
    /// Cost of the first extra core. Each further core costs
    /// <see cref="BotnetCoreCostGrowth"/> times the previous one.
    /// </summary>
    public double BotnetCoreBaseCost { get; init; } = 500_000;

    public double BotnetCoreCostGrowth { get; init; } = 1.48;

    public int BotnetMaxLevel { get; init; } = 200;

    public int BotnetMaxRam { get; init; } = 64;

    public int BotnetMaxCores { get; init; } = 16;

    // Randomness

    /// <summary>
    /// Default random seed used when deterministic game randomness
    /// is required.
    /// </summary>
    public int RandomSeed { get; init; } = 1337;
}