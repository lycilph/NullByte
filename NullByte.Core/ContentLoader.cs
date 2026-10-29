using System.Text.Json;

namespace NullByte.Core;

public static class ContentLoader
{
    public const string FileName = "content.json";

    // Case-insensitive lets the JSON use camelCase while records use PascalCase.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads content.json if present, otherwise the built-in default.</summary>
    public static GameContent Load()
        => File.Exists(FileName) ? FromJson(File.ReadAllText(FileName))
                                 : FromJson(DefaultContentJson);

    public static GameContent FromJson(string json)
    {
        var content = JsonSerializer.Deserialize<GameContent>(json, Options)
            ?? throw new InvalidContentException("Empty or invalid content JSON");
        content.Validate();
        return content;
    }

    /// <summary>Built-in default content. Copy to content.json to tweak balance.</summary>
    public const string DefaultContentJson = @"
{
  ""targets"": [
    { ""id"": ""webmail"",       ""name"": ""webmail.acme.local"",  ""minLevel"": 1,  ""durationTicks"": 30,  ""moneyReward"": 15,   ""xpReward"": 5 },
    { ""id"": ""forum-db"",      ""name"": ""forum.db.leak"",        ""minLevel"": 2,  ""durationTicks"": 45,  ""moneyReward"": 45,   ""xpReward"": 12 },
    { ""id"": ""corp-vpn"",      ""name"": ""corp-vpn.node07"",       ""minLevel"": 4,  ""durationTicks"": 60,  ""moneyReward"": 150,  ""xpReward"": 30 },
    { ""id"": ""city-bank"",     ""name"": ""city-bank.core"",        ""minLevel"": 7,  ""durationTicks"": 90,  ""moneyReward"": 600,  ""xpReward"": 80 },
    { ""id"": ""broker"",        ""name"": ""darknet-broker"",        ""minLevel"": 10, ""durationTicks"": 120, ""moneyReward"": 2500, ""xpReward"": 250 }
  ],
  ""upgrades"": [
    { ""id"": ""cpu"",    ""name"": ""CPU Core"",        ""description"": ""-5% hack duration per level"",       ""baseCost"": 50,   ""growthRate"": 1.15, ""maxLevel"": 50 },
    { ""id"": ""ram"",    ""name"": ""RAM Module"",      ""description"": ""+1 concurrent hack slot per level"", ""baseCost"": 200,  ""growthRate"": 1.35, ""maxLevel"": 10 },
    { ""id"": ""botnet"", ""name"": ""Botnet Node"",     ""description"": ""passive income, scales per level"",   ""baseCost"": 100,  ""growthRate"": 1.25, ""maxLevel"": 50 }
  ]
}
";
}