using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Nullbyte.Core;

// ---------------------------------------------------------------------------
// Content definitions: all game content lives in JSON, not in code.
// Adding a target or upgrade = editing content.json, never a recompile of
// engine logic.
// ---------------------------------------------------------------------------

/// <summary>
/// MaxMoney pins the drainable pool (Phase 4.5). 0 = auto: 8x MoneyReward,
/// which makes the FIRST hack pay ~the legacy flat reward, then decline.
/// </summary>
public sealed record TargetDef(
    string Id,
    string Name,
    int MinLevel,
    int DurationTicks,
    double MoneyReward,
    double XpReward,
    int RequiredCpu = 0,
    int RequiredRam = 0,
    double MaxMoney = 0);

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
/// Terminal flavor templates. Placeholders: {target}, {money}, {xp}.
/// Theme is content, not code — rewrite these in content.json to re-skin
/// the game without touching the engine.
/// </summary>
public sealed record FlavorContent(
    List<string> HackStart,
    List<string> HackComplete,
    List<string> Idle);

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
/// Timed bonus objective. Progress = StatValue gain from acceptance until
/// Goal is reached, within DurationTicks. Missing the deadline just loses
/// the reward — contracts are pure bonus.
/// </summary>
public sealed record ContractDef(
    string Id,
    string Name,
    string Description,
    string Stat,
    double Goal,
    int DurationTicks,
    double RewardMoney,
    double RewardXp);

/// <summary>
/// Prestige upgrade bought with 0-days. Survives identity burns.
/// Cost = BaseCost * GrowthRate^owned (in 0-days).
/// </summary>
public sealed record ImplantDef(
    string Id,
    string Name,
    string Description,
    double BaseCost,
    double GrowthRate,
    int MaxLevel);

public sealed class GameContent
{
    public List<TargetDef> Targets { get; init; } = new();
    public List<UpgradeDef> Upgrades { get; init; } = new();
    public List<PerkDef> Perks { get; init; } = new();
    public List<AchievementDef> Achievements { get; init; } = new();
    public List<ContractDef> Contracts { get; init; } = new();
    public List<ImplantDef> Implants { get; init; } = new();
    public FlavorContent Flavor { get; init; } = new(new(), new(), new());

    public TargetDef? FindTarget(string id) => Targets.Find(t => t.Id == id);
    public UpgradeDef? FindUpgrade(string id) => Upgrades.Find(u => u.Id == id);
    public PerkDef? FindPerk(string id) => Perks.Find(p => p.Id == id);
    public ContractDef? FindContract(string id) => Contracts.Find(c => c.Id == id);
    public ImplantDef? FindImplant(string id) => Implants.Find(i => i.Id == id);
}

public static class ContentLoader
{
    public const string FileName = "content.json";

    /// <summary>Loads content.json if present, otherwise the built-in default.</summary>
    public static GameContent Load()
        => File.Exists(FileName) ? FromJson(File.ReadAllText(FileName))
                                 : FromJson(DefaultContentJson);

    public static GameContent FromJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<JsonContent>(json, options)
            ?? throw new InvalidOperationException("Empty or invalid content JSON");

        return new GameContent
        {
            Targets = (doc.Targets ?? new()).ConvertAll(
                t => new TargetDef(t.Id, t.Name, t.MinLevel, t.DurationTicks,
                                   t.MoneyReward, t.XpReward,
                                   t.RequiredCpu, t.RequiredRam, t.MaxMoney)),
            Upgrades = (doc.Upgrades ?? new()).ConvertAll(
                u => new UpgradeDef(u.Id, u.Name, u.Description,
                                     u.BaseCost, u.GrowthRate, u.MaxLevel)),
            Perks = (doc.Perks ?? new()).ConvertAll(
                p => new PerkDef(p.Id, p.Name, p.Description, p.MaxLevel)),
            Flavor = new FlavorContent(
                doc.Flavor?.HackStart ?? new(),
                doc.Flavor?.HackComplete ?? new(),
                doc.Flavor?.Idle ?? new()),
            Achievements = (doc.Achievements ?? new()).ConvertAll(
                a => new AchievementDef(a.Id, a.Name, a.Description, a.Stat, a.Goal)),
            Contracts = (doc.Contracts ?? new()).ConvertAll(
                c => new ContractDef(c.Id, c.Name, c.Description, c.Stat,
                                    c.Goal, c.DurationTicks,
                                    c.RewardMoney, c.RewardXp)),
            Implants = (doc.Implants ?? new()).ConvertAll(
                i => new ImplantDef(i.Id, i.Name, i.Description,
                                    i.BaseCost, i.GrowthRate, i.MaxLevel)),
        };
    }

    private sealed class JsonContent
    {
        public List<JsonTarget>? Targets { get; set; }
        public List<JsonUpgrade>? Upgrades { get; set; }
        public List<JsonPerk>? Perks { get; set; }
        public List<JsonAchievement>? Achievements { get; set; }
        public List<JsonContract>? Contracts { get; set; }
        public List<JsonImplant>? Implants { get; set; }
        public JsonFlavor? Flavor { get; set; }
    }

    private sealed class JsonImplant
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double BaseCost { get; set; }
        public double GrowthRate { get; set; }
        public int MaxLevel { get; set; }
    }

    private sealed class JsonContract
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Stat { get; set; } = "";
        public double Goal { get; set; }
        public int DurationTicks { get; set; }
        public double RewardMoney { get; set; }
        public double RewardXp { get; set; }
    }

    private sealed class JsonAchievement
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Stat { get; set; } = "";
        public double Goal { get; set; }
    }

    private sealed class JsonFlavor
    {
        public List<string>? HackStart { get; set; }
        public List<string>? HackComplete { get; set; }
        public List<string>? Idle { get; set; }
    }

    private sealed class JsonTarget
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int MinLevel { get; set; }
        public int DurationTicks { get; set; }
        public double MoneyReward { get; set; }
        public double XpReward { get; set; }
        public int RequiredCpu { get; set; }
        public int RequiredRam { get; set; }
        public double MaxMoney { get; set; }
    }

    private sealed class JsonUpgrade
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double BaseCost { get; set; }
        public double GrowthRate { get; set; }
        public int MaxLevel { get; set; }
    }

    private sealed class JsonPerk
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int MaxLevel { get; set; }
    }

    /// <summary>Built-in default content. Copy to content.json to tweak balance.</summary>
    public const string DefaultContentJson = @"
{
  ""targets"": [
    { ""id"": ""webmail"",       ""name"": ""webmail.acme.local"",  ""minLevel"": 1,  ""durationTicks"": 30,  ""moneyReward"": 15,     ""xpReward"": 5 },
    { ""id"": ""forum-db"",      ""name"": ""forum.db.leak"",        ""minLevel"": 2,  ""durationTicks"": 45,  ""moneyReward"": 45,     ""xpReward"": 12 },
    { ""id"": ""pub-records"",   ""name"": ""public-records.gov"",   ""minLevel"": 3,  ""durationTicks"": 50,  ""moneyReward"": 90,     ""xpReward"": 20 },
    { ""id"": ""corp-vpn"",      ""name"": ""corp-vpn.node07"",       ""minLevel"": 4,  ""durationTicks"": 60,  ""moneyReward"": 150,    ""xpReward"": 30,   ""requiredCpu"": 5 },
    { ""id"": ""hotel-keys"",    ""name"": ""hotel-keycards"",        ""minLevel"": 5,  ""durationTicks"": 70,  ""moneyReward"": 260,    ""xpReward"": 45 },
    { ""id"": ""isp-router"",    ""name"": ""isp-router.edge"",       ""minLevel"": 6,  ""durationTicks"": 80,  ""moneyReward"": 420,    ""xpReward"": 60,   ""requiredCpu"": 10 },
    { ""id"": ""city-bank"",     ""name"": ""city-bank.core"",        ""minLevel"": 7,  ""durationTicks"": 90,  ""moneyReward"": 600,    ""xpReward"": 80,   ""requiredRam"": 3 },
    { ""id"": ""wallet-farm"",   ""name"": ""crypto-wallet.farm"",    ""minLevel"": 8,  ""durationTicks"": 100, ""moneyReward"": 900,    ""xpReward"": 110,  ""requiredCpu"": 15 },
    { ""id"": ""hospital-db"",   ""name"": ""hospital-records"",      ""minLevel"": 9,  ""durationTicks"": 110, ""moneyReward"": 1300,   ""xpReward"": 140 },
    { ""id"": ""sat-uplink"",    ""name"": ""sat-uplink.alpha"",      ""minLevel"": 10, ""durationTicks"": 120, ""moneyReward"": 1900,   ""xpReward"": 180,  ""requiredCpu"": 20, ""requiredRam"": 4 },
    { ""id"": ""broker"",        ""name"": ""darknet-broker"",        ""minLevel"": 10, ""durationTicks"": 120, ""moneyReward"": 2500,   ""xpReward"": 250 },
    { ""id"": ""bounty-board"",  ""name"": ""bounty-board"",          ""minLevel"": 11, ""durationTicks"": 130, ""moneyReward"": 3400,   ""xpReward"": 300 },
    { ""id"": ""stock-ex"",      ""name"": ""stock-exchange.dm"",     ""minLevel"": 12, ""durationTicks"": 150, ""moneyReward"": 5200,   ""xpReward"": 400,  ""requiredRam"": 6 },
    { ""id"": ""fed-reserve"",   ""name"": ""fed-reserve.node"",      ""minLevel"": 13, ""durationTicks"": 180, ""moneyReward"": 9000,   ""xpReward"": 600,  ""requiredCpu"": 30 },
    { ""id"": ""orbital-relay"", ""name"": ""orbital-relay"",         ""minLevel"": 14, ""durationTicks"": 220, ""moneyReward"": 16000,  ""xpReward"": 850,  ""requiredCpu"": 35, ""requiredRam"": 8 },
    { ""id"": ""ghost-server"",  ""name"": ""ghost-server.42"",       ""minLevel"": 15, ""durationTicks"": 260, ""moneyReward"": 30000,  ""xpReward"": 1200 },
    { ""id"": ""blacksite"",     ""name"": ""blacksite.vault"",       ""minLevel"": 16, ""durationTicks"": 300, ""moneyReward"": 60000,  ""xpReward"": 2000, ""requiredCpu"": 45, ""requiredRam"": 10 },
    { ""id"": ""singularity"",   ""name"": ""singularity-core"",      ""minLevel"": 18, ""durationTicks"": 360, ""moneyReward"": 150000, ""xpReward"": 4000, ""requiredCpu"": 50, ""requiredRam"": 10 }
  ],
  ""upgrades"": [
    { ""id"": ""cpu"",    ""name"": ""CPU Core"",        ""description"": ""-5% hack duration per level"",            ""baseCost"": 50,   ""growthRate"": 1.15, ""maxLevel"": 50 },
    { ""id"": ""ram"",    ""name"": ""RAM Module"",      ""description"": ""+1 concurrent hack slot per level"",      ""baseCost"": 200,  ""growthRate"": 1.35, ""maxLevel"": 10 },
    { ""id"": ""botnet"", ""name"": ""Botnet Node"",     ""description"": ""passive income, scales per level"",       ""baseCost"": 100,  ""growthRate"": 1.25, ""maxLevel"": 50 }
  ],
  ""perks"": [
    { ""id"": ""overclock"", ""name"": ""Overclock"",     ""description"": ""-3% hack duration per level"",   ""maxLevel"": 10 },
    { ""id"": ""ransom"",    ""name"": ""Ransomware"",    ""description"": ""+10% hack money per level"",     ""maxLevel"": 10 },
    { ""id"": ""datamine"",  ""name"": ""Data Mining"",   ""description"": ""+10% XP gain per level"",         ""maxLevel"": 10 },
    { ""id"": ""scavenger"", ""name"": ""Scavenger"",     ""description"": ""+10% botnet income per level"",   ""maxLevel"": 10 },
    { ""id"": ""multitask"", ""name"": ""Multitasking"",  ""description"": ""+1 hack slot per level"",         ""maxLevel"": 3 }
  ],
  ""flavor"": {
    ""hackStart"": [
      ""Routing through {target} — proxy chain established"",
      ""Injecting payload into {target}"",
      ""Scanning {target} for open ports... found 3"",
      ""Social-engineering the admin of {target}"",
      ""Squatting on {target}'s maintenance backdoor""
    ],
    ""hackComplete"": [
      ""Root shell on {target} — dumped records worth ${money}"",
      ""{target} owned. Credentials sold for ${money}"",
      ""Siphoned ${money} out of {target}"",
      ""Backdoor planted on {target}. Extracted ${money} and {xp} XP""
    ],
    ""idle"": [
      ""A rival crew pinged your node. They moved on."",
      ""Your botnet nodes check in. All quiet."",
      ""ISP throttling detected in your sector."",
      ""You spot a watcher in IRC. Probably nothing."",
      ""Rain on the window. The rig hums along."",
      ""Someone is port-scanning the neighborhood router."",
      ""The vending machine downstairs is out of energy drinks again.""
    ]
  },
  ""achievements"": [
    { ""id"": ""first-blood"",  ""name"": ""First Blood"",        ""description"": ""Complete your first hack"",   ""stat"": ""hacks"",                     ""goal"": 1 },
    { ""id"": ""hacks-100"",    ""name"": ""Serial Intruder"",    ""description"": ""Complete 100 hacks"",         ""stat"": ""hacks"",                     ""goal"": 100 },
    { ""id"": ""money-10k"",    ""name"": ""Payday"",             ""description"": ""Earn $10,000 total"",         ""stat"": ""moneyEarned"",              ""goal"": 10000 },
    { ""id"": ""level-5"",      ""name"": ""Moving Up"",          ""description"": ""Reach level 5"",              ""stat"": ""level"",                     ""goal"": 5 },
    { ""id"": ""level-10"",     ""name"": ""Ghost in the Wires"", ""description"": ""Reach level 10"",             ""stat"": ""level"",                     ""goal"": 10 },
    { ""id"": ""upgrader"",     ""name"": ""Hardware Nerd"",      ""description"": ""Buy 10 rig upgrades"",       ""stat"": ""upgradesBought"",            ""goal"": 10 },
    { ""id"": ""bank-job"",     ""name"": ""Bank Job"",           ""description"": ""Hack city-bank"",            ""stat"": ""targetHacks:city-bank"",     ""goal"": 1 },
    { ""id"": ""deep-end"",     ""name"": ""Too Deep"",           ""description"": ""Hack ghost-server.42"",      ""stat"": ""targetHacks:ghost-server"",  ""goal"": 1 }
  ],
  ""contracts"": [
    { ""id"": ""warmup"",    ""name"": ""Warm-Up"",          ""description"": ""Complete 5 hacks"",             ""stat"": ""hacks"",                 ""goal"": 5,    ""durationTicks"": 1200, ""rewardMoney"": 500,   ""rewardXp"": 50 },
    { ""id"": ""spree"",     ""name"": ""Spree"",            ""description"": ""Complete 10 hacks fast"",       ""stat"": ""hacks"",                 ""goal"": 10,   ""durationTicks"": 900,  ""rewardMoney"": 1500,  ""rewardXp"": 120 },
    { ""id"": ""payday"",    ""name"": ""Payday"",           ""description"": ""Earn $2,000"",                  ""stat"": ""moneyEarned"",           ""goal"": 2000, ""durationTicks"": 1800, ""rewardMoney"": 800,   ""rewardXp"": 80 },
    { ""id"": ""cleaner"",   ""name"": ""Hardware Fetish"",  ""description"": ""Buy 3 rig upgrades"",           ""stat"": ""upgradesBought"",        ""goal"": 3,    ""durationTicks"": 1500, ""rewardMoney"": 1000,  ""rewardXp"": 60 },
    { ""id"": ""bankrun"",   ""name"": ""Bank Run"",         ""description"": ""Hack city-bank 3 times"",       ""stat"": ""targetHacks:city-bank"", ""goal"": 3,    ""durationTicks"": 2400, ""rewardMoney"": 4000,  ""rewardXp"": 300 },
    { ""id"": ""prodigy"",   ""name"": ""Prodigy"",          ""description"": ""Level up twice"",               ""stat"": ""level"",                ""goal"": 2,    ""durationTicks"": 3000, ""rewardMoney"": 2500,  ""rewardXp"": 200 }
  ],
  ""implants"": [
    { ""id"": ""notoriety"", ""name"": ""Dark Web Notoriety"", ""description"": ""+10% hack money per level"",   ""baseCost"": 1, ""growthRate"": 2,   ""maxLevel"": 15 },
    { ""id"": ""blackice"",  ""name"": ""Black ICE"",          ""description"": ""+10% XP per level"",           ""baseCost"": 1, ""growthRate"": 2,   ""maxLevel"": 15 },
    { ""id"": ""neural"",    ""name"": ""Neural Overclock"",   ""description"": ""-3% hack duration per level"", ""baseCost"": 2, ""growthRate"": 2,   ""maxLevel"": 10 },
    { ""id"": ""burner"",    ""name"": ""Burner Fund"",        ""description"": ""seed money on identity burn"",  ""baseCost"": 3, ""growthRate"": 2.5, ""maxLevel"": 10 }
  ]
}
";
}

/// <summary>Classic idler cost curve: cost = base * growth^owned.</summary>
public static class CostMath
{
    public static double UpgradeCost(UpgradeDef def, int owned)
        => def.BaseCost * Math.Pow(def.GrowthRate, owned);
}