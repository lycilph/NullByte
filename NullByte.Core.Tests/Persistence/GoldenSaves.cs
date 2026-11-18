namespace NullByte.Core.Tests.Persistence;

/// <summary>
/// Saves in each released format, kept exactly as written. Never edit an existing entry:
/// they are what proves that old saves still load.
/// </summary>
internal static class GoldenSaves
{
    public const string V1 = """
{
  "version": 1,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeHostname": "home",
    "currentHostname": "n00dles",
    "money": 0.30000000000000004,
    "hackingExperience": 0.3333333333333333,
    "discoveredHostnames": [
      "foodnstuff",
      "home",
      "n00dles"
    ]
  },
  "servers": [
    {
      "hostname": "foodnstuff",
      "maximumMoney": 100000,
      "money": 60000,
      "baseDifficulty": 5,
      "minimumSecurity": 2,
      "security": 5,
      "requiredHackingLevel": 5,
      "growthParameter": 25,
      "maximumRam": 8,
      "hasRootAccess": false
    },
    {
      "hostname": "home",
      "maximumMoney": 0,
      "money": 0,
      "baseDifficulty": 1,
      "minimumSecurity": 1,
      "security": 1,
      "requiredHackingLevel": 1,
      "growthParameter": 1,
      "maximumRam": 8,
      "hasRootAccess": true
    },
    {
      "hostname": "n00dles",
      "maximumMoney": 50000,
      "money": 24999.666666666668,
      "baseDifficulty": 3,
      "minimumSecurity": 1,
      "security": 3.002,
      "requiredHackingLevel": 1,
      "growthParameter": 30,
      "maximumRam": 4,
      "hasRootAccess": true
    },
    {
      "hostname": "zero-day",
      "maximumMoney": 1000000,
      "money": 500000,
      "baseDifficulty": 40,
      "minimumSecurity": 7.54,
      "security": 66.5,
      "requiredHackingLevel": 50,
      "growthParameter": 10,
      "maximumRam": 16,
      "hasRootAccess": false
    }
  ],
  "connections": [
    {
      "from": "foodnstuff",
      "to": "home"
    },
    {
      "from": "foodnstuff",
      "to": "n00dles"
    },
    {
      "from": "foodnstuff",
      "to": "zero-day"
    },
    {
      "from": "home",
      "to": "n00dles"
    }
  ],
  "botnet": {
    "totalEarned": 12.345,
    "nodes": [
      {
        "level": 2,
        "ram": 2,
        "cores": 2
      },
      {
        "level": 1,
        "ram": 1,
        "cores": 1
      }
    ]
  },
  "files": [
    {
      "hostname": "home",
      "name": "empty.cx",
      "content": ""
    },
    {
      "hostname": "home",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n"
    },
    {
      "hostname": "n00dles",
      "name": "hack.cx",
      "content": "await nb.Hack();"
    }
  ],
  "processes": [
    {
      "filename": "weaken.cx",
      "hostname": "home",
      "args": [],
      "threads": 1
    },
    {
      "filename": "hack.cx",
      "hostname": "n00dles",
      "args": [
        "foodnstuff",
        "two words"
      ],
      "threads": 2
    }
  ]
}
""";
}
