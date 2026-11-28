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
      "to": "foodnstuff"
    },
    {
      "from": "home",
      "to": "n00dles"
    },
    {
      "from": "n00dles",
      "to": "foodnstuff"
    },
    {
      "from": "n00dles",
      "to": "home"
    },
    {
      "from": "zero-day",
      "to": "foodnstuff"
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

    public const string V2 = """
{
  "version": 2,
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
      "to": "foodnstuff"
    },
    {
      "from": "home",
      "to": "n00dles"
    },
    {
      "from": "n00dles",
      "to": "foodnstuff"
    },
    {
      "from": "n00dles",
      "to": "home"
    },
    {
      "from": "zero-day",
      "to": "foodnstuff"
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
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "home",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "n00dles",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
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

    public const string V3 = """
{
  "version": 3,
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
      "to": "foodnstuff"
    },
    {
      "from": "home",
      "to": "n00dles"
    },
    {
      "from": "n00dles",
      "to": "foodnstuff"
    },
    {
      "from": "n00dles",
      "to": "home"
    },
    {
      "from": "zero-day",
      "to": "foodnstuff"
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
  "directories": [],
  "files": [
    {
      "hostname": "home",
      "name": "empty.cx",
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "home",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "n00dles",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
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

    public const string V4 = """
{
  "version": 4,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeHostname": "home",
    "currentHostname": "n00dles",
    "money": 0.30000000000000004,
    "skills": {
      "hacking": 0.3333333333333333,
      "strength": 0,
      "defense": 0,
      "dexterity": 0,
      "agility": 0,
      "charisma": 0
    },
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
      "to": "foodnstuff"
    },
    {
      "from": "home",
      "to": "n00dles"
    },
    {
      "from": "n00dles",
      "to": "foodnstuff"
    },
    {
      "from": "n00dles",
      "to": "home"
    },
    {
      "from": "zero-day",
      "to": "foodnstuff"
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
  "employment": {
    "jobs": [],
    "reputation": []
  },
  "activity": null,
  "directories": [],
  "files": [
    {
      "hostname": "home",
      "name": "empty.cx",
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "home",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "n00dles",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
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

    public const string V5 = """
{
  "version": 5,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeHostname": "home",
    "currentHostname": "n00dles",
    "money": 0.30000000000000004,
    "skills": {
      "hacking": 0.3333333333333333,
      "strength": 0,
      "defense": 0,
      "dexterity": 0,
      "agility": 0,
      "charisma": 0
    },
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
      "hasRootAccess": false,
      "hasBackdoor": false
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
      "hasRootAccess": true,
      "hasBackdoor": false
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
      "hasRootAccess": true,
      "hasBackdoor": false
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
      "hasRootAccess": false,
      "hasBackdoor": false
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
      "to": "foodnstuff"
    },
    {
      "from": "home",
      "to": "n00dles"
    },
    {
      "from": "n00dles",
      "to": "foodnstuff"
    },
    {
      "from": "n00dles",
      "to": "home"
    },
    {
      "from": "zero-day",
      "to": "foodnstuff"
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
  "employment": {
    "jobs": [],
    "reputation": []
  },
  "activity": null,
  "directories": [],
  "files": [
    {
      "hostname": "home",
      "name": "empty.cx",
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "home",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "hostname": "n00dles",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
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

    public const string V6 = """
{
  "version": 6,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
    "currentServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
    "money": 0.30000000000000004,
    "skills": {
      "hacking": 0.3333333333333333,
      "strength": 0,
      "defense": 0,
      "dexterity": 0,
      "agility": 0,
      "charisma": 0
    },
    "discoveredServerIds": [
      "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "98a3870e-58cb-5079-93b5-bb06f209320b"
    ]
  },
  "servers": [
    {
      "id": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "hostname": "foodnstuff",
      "maximumMoney": 100000,
      "money": 60000,
      "baseDifficulty": 5,
      "minimumSecurity": 2,
      "security": 5,
      "requiredHackingLevel": 5,
      "growthParameter": 25,
      "maximumRam": 8,
      "hasRootAccess": false,
      "hasBackdoor": false
    },
    {
      "id": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "hostname": "home",
      "maximumMoney": 0,
      "money": 0,
      "baseDifficulty": 1,
      "minimumSecurity": 1,
      "security": 1,
      "requiredHackingLevel": 1,
      "growthParameter": 1,
      "maximumRam": 8,
      "hasRootAccess": true,
      "hasBackdoor": false
    },
    {
      "id": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "hostname": "n00dles",
      "maximumMoney": 50000,
      "money": 24999.666666666668,
      "baseDifficulty": 3,
      "minimumSecurity": 1,
      "security": 3.002,
      "requiredHackingLevel": 1,
      "growthParameter": 30,
      "maximumRam": 4,
      "hasRootAccess": true,
      "hasBackdoor": false
    },
    {
      "id": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "hostname": "zero-day",
      "maximumMoney": 1000000,
      "money": 500000,
      "baseDifficulty": 40,
      "minimumSecurity": 7.54,
      "security": 66.5,
      "requiredHackingLevel": 50,
      "growthParameter": 10,
      "maximumRam": 16,
      "hasRootAccess": false,
      "hasBackdoor": false
    }
  ],
  "connections": [
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
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
  "employment": {
    "jobs": [],
    "reputation": []
  },
  "activity": null,
  "directories": [],
  "files": [
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "empty.cx",
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    }
  ],
  "processes": [
    {
      "filename": "weaken.cx",
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "args": [],
      "threads": 1
    },
    {
      "filename": "hack.cx",
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "args": [
        "foodnstuff",
        "two words"
      ],
      "threads": 2
    }
  ]
}
""";

    public const string V7 = """
{
  "version": 7,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
    "currentServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
    "money": 0.30000000000000004,
    "skills": {
      "hacking": 0.3333333333333333,
      "strength": 0,
      "defense": 0,
      "dexterity": 0,
      "agility": 0,
      "charisma": 0
    },
    "discoveredServerIds": [
      "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "98a3870e-58cb-5079-93b5-bb06f209320b"
    ]
  },
  "servers": [
    {
      "id": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "hostname": "foodnstuff",
      "maximumMoney": 100000,
      "money": 60000,
      "baseDifficulty": 5,
      "minimumSecurity": 2,
      "security": 5,
      "requiredHackingLevel": 5,
      "growthParameter": 25,
      "maximumRam": 8,
      "hasRootAccess": false,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "hostname": "home",
      "maximumMoney": 0,
      "money": 0,
      "baseDifficulty": 1,
      "minimumSecurity": 1,
      "security": 1,
      "requiredHackingLevel": 1,
      "growthParameter": 1,
      "maximumRam": 8,
      "hasRootAccess": true,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "hostname": "n00dles",
      "maximumMoney": 50000,
      "money": 24999.666666666668,
      "baseDifficulty": 3,
      "minimumSecurity": 1,
      "security": 3.002,
      "requiredHackingLevel": 1,
      "growthParameter": 30,
      "maximumRam": 4,
      "hasRootAccess": true,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "hostname": "zero-day",
      "maximumMoney": 1000000,
      "money": 500000,
      "baseDifficulty": 40,
      "minimumSecurity": 7.54,
      "security": 66.5,
      "requiredHackingLevel": 50,
      "growthParameter": 10,
      "maximumRam": 16,
      "hasRootAccess": false,
      "hasBackdoor": false,
      "isCloudServer": false
    }
  ],
  "connections": [
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
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
  "employment": {
    "jobs": [],
    "reputation": []
  },
  "activity": null,
  "directories": [],
  "files": [
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "empty.cx",
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    }
  ],
  "processes": [
    {
      "filename": "weaken.cx",
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "args": [],
      "threads": 1
    },
    {
      "filename": "hack.cx",
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "args": [
        "foodnstuff",
        "two words"
      ],
      "threads": 2
    }
  ]
}
""";

    public const string V8 = """
{
  "version": 8,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
    "currentServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
    "money": 0.30000000000000004,
    "skills": {
      "hacking": 0.3333333333333333,
      "strength": 0,
      "defense": 0,
      "dexterity": 0,
      "agility": 0,
      "charisma": 0
    },
    "discoveredServerIds": [
      "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "98a3870e-58cb-5079-93b5-bb06f209320b"
    ]
  },
  "servers": [
    {
      "id": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "hostname": "foodnstuff",
      "maximumMoney": 100000,
      "money": 60000,
      "baseDifficulty": 5,
      "minimumSecurity": 2,
      "security": 5,
      "requiredHackingLevel": 5,
      "growthParameter": 25,
      "maximumRam": 8,
      "hasRootAccess": false,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "hostname": "home",
      "maximumMoney": 0,
      "money": 0,
      "baseDifficulty": 1,
      "minimumSecurity": 1,
      "security": 1,
      "requiredHackingLevel": 1,
      "growthParameter": 1,
      "maximumRam": 8,
      "hasRootAccess": true,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "hostname": "n00dles",
      "maximumMoney": 50000,
      "money": 24999.666666666668,
      "baseDifficulty": 3,
      "minimumSecurity": 1,
      "security": 3.002,
      "requiredHackingLevel": 1,
      "growthParameter": 30,
      "maximumRam": 4,
      "hasRootAccess": true,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "hostname": "zero-day",
      "maximumMoney": 1000000,
      "money": 500000,
      "baseDifficulty": 40,
      "minimumSecurity": 7.54,
      "security": 66.5,
      "requiredHackingLevel": 50,
      "growthParameter": 10,
      "maximumRam": 16,
      "hasRootAccess": false,
      "hasBackdoor": false,
      "isCloudServer": false
    }
  ],
  "connections": [
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
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
  "employment": {
    "jobs": [],
    "reputation": []
  },
  "activity": null,
  "directories": [],
  "files": [
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "empty.cx",
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    }
  ],
  "processes": [
    {
      "filename": "weaken.cx",
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "args": [],
      "threads": 1
    },
    {
      "filename": "hack.cx",
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "args": [
        "foodnstuff",
        "two words"
      ],
      "threads": 2
    }
  ],
  "narrative": {
    "mode": 1,
    "unlockedCapabilities": [],
    "completedMilestones": [],
    "deliveredMessages": []
  }
}
""";

    public const string V9 = """
{
  "version": 9,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
    "currentServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
    "money": 0.30000000000000004,
    "skills": {
      "hacking": 0.3333333333333333,
      "strength": 0,
      "defense": 0,
      "dexterity": 0,
      "agility": 0,
      "charisma": 0
    },
    "discoveredServerIds": [
      "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "98a3870e-58cb-5079-93b5-bb06f209320b"
    ]
  },
  "servers": [
    {
      "id": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "hostname": "foodnstuff",
      "maximumMoney": 100000,
      "money": 60000,
      "baseDifficulty": 5,
      "minimumSecurity": 2,
      "security": 5,
      "requiredHackingLevel": 5,
      "growthParameter": 25,
      "maximumRam": 8,
      "hasRootAccess": false,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "hostname": "home",
      "maximumMoney": 0,
      "money": 0,
      "baseDifficulty": 1,
      "minimumSecurity": 1,
      "security": 1,
      "requiredHackingLevel": 1,
      "growthParameter": 1,
      "maximumRam": 8,
      "hasRootAccess": true,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "hostname": "n00dles",
      "maximumMoney": 50000,
      "money": 24999.666666666668,
      "baseDifficulty": 3,
      "minimumSecurity": 1,
      "security": 3.002,
      "requiredHackingLevel": 1,
      "growthParameter": 30,
      "maximumRam": 4,
      "hasRootAccess": true,
      "hasBackdoor": false,
      "isCloudServer": false
    },
    {
      "id": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "hostname": "zero-day",
      "maximumMoney": 1000000,
      "money": 500000,
      "baseDifficulty": 40,
      "minimumSecurity": 7.54,
      "security": 66.5,
      "requiredHackingLevel": 50,
      "growthParameter": 10,
      "maximumRam": 16,
      "hasRootAccess": false,
      "hasBackdoor": false,
      "isCloudServer": false
    }
  ],
  "connections": [
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f",
      "toServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "toServerId": "98a3870e-58cb-5079-93b5-bb06f209320b"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
    },
    {
      "fromServerId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "toServerId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e"
    },
    {
      "fromServerId": "e68d08ef-e8d3-5b8b-9bd3-001535c25142",
      "toServerId": "83d7acb2-037f-5621-bd1d-c708349ae12f"
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
  "employment": {
    "jobs": [],
    "reputation": []
  },
  "activity": null,
  "directories": [],
  "files": [
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "empty.cx",
      "content": "",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "name": "hack.cx",
      "content": "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    },
    {
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "name": "hack.cx",
      "content": "await nb.Hack();",
      "created": "2026-11-17T10:30:00+00:00",
      "modified": "2026-11-17T10:30:00+00:00"
    }
  ],
  "processes": [
    {
      "filename": "weaken.cx",
      "serverId": "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e",
      "args": [],
      "threads": 1
    },
    {
      "filename": "hack.cx",
      "serverId": "98a3870e-58cb-5079-93b5-bb06f209320b",
      "args": [
        "foodnstuff",
        "two words"
      ],
      "threads": 2
    }
  ],
  "narrative": {
    "mode": 1,
    "unlockedCapabilities": [],
    "completedMilestones": [],
    "deliveredMessages": [],
    "discoveredDiscoveries": []
  }
}
""";
}