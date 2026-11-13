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

    // Hacking chance

    /// <summary>
    /// Player skill is multiplied by this value when calculating hack chance.
    /// Mirrors Bitburner's hackFactor.
    /// </summary>
    public double HackChanceSkillFactor { get; init; } = 1.75;

    /// <summary>
    /// Global multiplier applied to hacking success chance.
    /// Reserved for later progression systems.
    /// </summary>
    public double HackingChanceMultiplier { get; init; } = 1.0;

    /// <summary>
    /// Intelligence contribution to hacking formulas.
    /// NullByte does not currently model intelligence, so this defaults to one.
    /// </summary>
    public double HackingIntelligenceBonus { get; init; } = 1.0;


    // Hacking money

    /// <summary>
    /// Divisor used when calculating the percentage of server money
    /// stolen by one successful hacking thread.
    /// </summary>
    public double HackBalanceFactor { get; init; } = 240.0;

    /// <summary>
    /// Global multiplier applied to money stolen by hacking.
    /// Reserved for later progression systems.
    /// </summary>
    public double HackingMoneyMultiplier { get; init; } = 1.0;


    // Security effects

    /// <summary>
    /// Security added by each hacking thread when a hack completes.
    /// </summary>
    public double HackSecurityIncrease { get; init; } = 0.002;

    /// <summary>
    /// Security added by each growing thread when a grow completes.
    /// </summary>
    public double GrowSecurityIncrease { get; init; } = 0.004;

    /// <summary>
    /// Security removed by each weakening thread when a weaken completes.
    /// </summary>
    public double WeakenSecurityDecrease { get; init; } = 0.05;


    // Action durations

    /// <summary>
    /// Multiplier applied to Bitburner's calculated hacking skill factor.
    /// </summary>
    public double HackTimeMultiplier { get; init; } = 5.0;

    /// <summary>
    /// Weight of server difficulty in the hacking-time formula.
    /// </summary>
    public double HackDifficultyFactor { get; init; } = 2.5;

    /// <summary>
    /// Constant added to the difficulty side of the hacking-time formula.
    /// </summary>
    public double HackDifficultyBase { get; init; } = 500.0;

    /// <summary>
    /// Constant added to player skill in the hacking-time formula.
    /// </summary>
    public double HackSkillBase { get; init; } = 50.0;

    /// <summary>
    /// Global multiplier controlling hacking speed.
    /// Reserved for later progression systems.
    /// </summary>
    public double HackingSpeedMultiplier { get; init; } = 1.0;

    /// <summary>
    /// Grow duration relative to calculated hacking duration.
    /// </summary>
    public double GrowDurationMultiplier { get; init; } = 3.2;

    /// <summary>
    /// Weaken duration relative to calculated hacking duration.
    /// </summary>
    public double WeakenDurationMultiplier { get; init; } = 4.0;


    // Experience and skill progression

    /// <summary>
    /// Multiplier applied when deriving hacking skill from experience.
    /// </summary>
    public double HackingSkillMultiplier { get; init; } = 1.0;

    /// <summary>
    /// Logarithmic multiplier in the skill-from-experience formula.
    /// </summary>
    public double SkillLogarithmMultiplier { get; init; } = 32.0;

    /// <summary>
    /// Experience offset in the skill-from-experience formula.
    /// </summary>
    public double SkillExperienceOffset { get; init; } = 534.6;

    /// <summary>
    /// Level offset subtracted by the skill-from-experience formula.
    /// </summary>
    public double SkillLevelOffset { get; init; } = 200.0;

    /// <summary>
    /// Base hacking experience awarded by hack, grow, and weaken.
    /// </summary>
    public double BaseHackingExperience { get; init; } = 3.0;

    /// <summary>
    /// A percentage of the experience a failed hack awards
    /// </summary>
    public double FailedHackExperienceFactor { get; init; } = 0.25;

    /// <summary>
    /// Amount of additional experience awarded per point of
    /// server base difficulty.
    /// </summary>
    public double HackingExperienceDifficultyFactor { get; init; } = 0.3;

    /// <summary>
    /// Global multiplier applied to hacking experience.
    /// Reserved for later progression systems.
    /// </summary>
    public double HackingExperienceMultiplier { get; init; } = 1.0;


    // Growth

    /// <summary>
    /// Base increment used by the server-growth formula.
    /// </summary>
    public double ServerBaseGrowthIncrement { get; init; } = 0.03;

    /// <summary>
    /// Maximum growth rate per server-growth cycle.
    /// </summary>
    public double ServerMaximumGrowthRate { get; init; } = 1.0035;

    /// <summary>
    /// Global multiplier applied to server growth.
    /// Reserved for later progression systems.
    /// </summary>
    public double ServerGrowthMultiplier { get; init; } = 1.0;

    // Botnet
    #region botnet

    /// <summary>
    /// Multiplier applied to all botnet production. Use it to tune the
    /// botnet's share of the economy without touching the cost curves.
    /// </summary>
    public double BotnetProductionMultiplier { get; init; } = 1.0;
    
    /// <summary>
    /// Multiplier applied to botnet node and upgrade costs.
    /// </summary>
    public double BotnetPurchaseCostMultiplier { get; init; } = 1.0;

    /// <summary>
    /// Money produced per second by each level of a node, before the RAM
    /// and core bonuses.
    /// </summary>
    public double BotnetProductionPerLevel { get; init; } = 1.5;

    /// <summary>
    /// Production grows by this factor for every point of RAM above one.
    /// </summary>
    public double BotnetRamProductionBase { get; init; } = 1.035;

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
    #endregion
}