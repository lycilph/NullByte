namespace NullByte.Core;

// ---------------------------------------------------------------------------
// Content definitions: all game content lives in JSON, not in code.
// Adding a target or upgrade = editing content.json, never a recompile of
// engine logic.
// ---------------------------------------------------------------------------

public sealed record TargetDef(
    string Id,
    string Name,
    int MinLevel,
    int DurationTicks,
    double MoneyReward,
    double XpReward, 
    int RequiredCpu = 0,
    int RequiredRam = 0);

public sealed record UpgradeDef(
    string Id,
    string Name,
    string Description,
    double BaseCost,
    double GrowthRate,
    int MaxLevel);

public sealed record PerkDef(
    string Id,
    string Name,
    string Description,
    int MaxLevel);

/// <summary>
/// Achievement: unlocks when StatValue(state, Stat) reaches Goal.
/// Stat keys: "hacks", "moneyEarned", "level", "upgradesBought",
/// "targetHacks:&lt;targetId&gt;".
/// </summary>
public sealed record AchievementDef(
    string Id,
    string Name,
    string Description,
    string Stat,
    double Goal);

/// <summary>
/// Terminal flavor templates. Placeholders: {target}, {money}, {xp}.
/// Theme is content, not code — rewrite these in content.json to re-skin
/// the game without touching the engine.
/// </summary>
public sealed record FlavorContent(
    List<string> HackStart,
    List<string> HackComplete,
    List<string> Idle);

public sealed class GameContent
{
    public List<TargetDef> Targets { get; init; } = [];
    public List<UpgradeDef> Upgrades { get; init; } = [];
    public List<PerkDef> Perks { get; init; } = [];
    public List<AchievementDef> Achievements { get; init; } = [];
    public FlavorContent Flavor { get; init; } = new([], [], []);

    public TargetDef? FindTarget(string id) => Targets.Find(t => t.Id == id);
    public UpgradeDef? FindUpgrade(string id) => Upgrades.Find(u => u.Id == id);
    public PerkDef? FindPerk(string id) => Perks.Find(u => u.Id == id);

    /// <summary>
    /// Fails fast on content mistakes (duplicate ids, bad values) instead of
    /// letting them surface as weird behavior hours into a playthrough.
    /// </summary>
    public void Validate()
    {
        foreach (var group in new[] { Targets.Select(t => (id: t.Id, name: t.Name)),
                                      Upgrades.Select(u => (id: u.Id, name: u.Name)),
                                      Perks.Select(u => (id: u.Id, name: u.Name))})
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, name) in group)
            {
                if (!ids.Add(id))
                    throw new InvalidContentException($"Duplicate content id '{id}'.");
            }
        }
        foreach (var t in Targets)
            if (t.DurationTicks < 1 || t.MoneyReward < 0 || t.XpReward < 0)
                throw new InvalidContentException($"Target '{t.Id}' has invalid values.");
        foreach (var u in Upgrades)
            if (u.BaseCost < 0 || u.GrowthRate <= 1 || u.MaxLevel < 1)
                throw new InvalidContentException($"Upgrade '{u.Id}' has invalid values.");
        foreach (var p in Perks)
            if (p.MaxLevel < 1)
                throw new InvalidContentException($"Perk '{p.Id}' has invalid values.");
    }
}