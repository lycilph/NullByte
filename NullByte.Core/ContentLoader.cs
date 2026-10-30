using System.Text.Json;

namespace NullByte.Core;

public static class ContentLoader
{
    public const string FileName = "content.json";

    /// <summary>Loads content.json if present, otherwise the built-in default.</summary>
    public static GameContent Load()
        => File.Exists(FileName) ? FromJson(File.ReadAllText(FileName))
                                 : FromJson(DefaultContentJson);

    public static GameContent FromJson(string json)
    {
        // Case-insensitive lets the JSON use camelCase while records use PascalCase.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var content = JsonSerializer.Deserialize<GameContent>(json, options)
            ?? throw new InvalidContentException("Empty or invalid content JSON");
        content.Validate();
        return content;
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
    { ""id"": ""prodigy"",   ""name"": ""Prodigy"",          ""description"": ""Level up twice"",               ""stat"": ""level"",                 ""goal"": 2,    ""durationTicks"": 3000, ""rewardMoney"": 2500,  ""rewardXp"": 200 }
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
      ""Root shell on {target} — dumped records worth ${money} ({xp} XP)"",
      ""{target} owned. Credentials sold for ${money} ({xp} XP)"",
      ""Siphoned ${money} out of {target} ({xp} XP)"",
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
  }
}
";
}