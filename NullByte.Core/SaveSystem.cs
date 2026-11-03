using System.Text.Json;

namespace NullByte.Core;

// ---------------------------------------------------------------------------
// Save/load with schema versioning and offline elapsed-time capture.
// The caller turns offlineElapsed into FastForward ticks (capped here).
// ---------------------------------------------------------------------------

public static class SaveSystem
{
    public const string SaveFile = "save.json";
    public static readonly TimeSpan OfflineCap = TimeSpan.FromHours(8);

    private static readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
    };

    public static void Save(GameSession session, string path = SaveFile)
    {
        var tmp = path + ".tmp";
        var data = session.CaptureSaveData();
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, options));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Returns the restored state plus how long the game was closed.
    /// Returns null if there is no save or the version is unsupported
    /// (add a migration step here when SchemaVersion bumps).
    /// </summary>
    public static (GameState State, TimeSpan OfflineElapsed)? Load(string path = SaveFile)
    {
        if (!File.Exists(path))
            return null;

        SaveData? data;
        try
        {
            data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
        if (data is null || data.SchemaVersion != 1)
            return null;

        var state = new GameState
        {
            Money = data.Money,
            Xp = data.Xp,
            Level = data.Level,
            SkillPoints = data.SkillPoints,
            ZeroDays = data.ZeroDays,
            GameTick = data.GameTick,
            UpgradeLevels = data.UpgradeLevels ?? [],
            PerkLevels = data.PerkLevels ?? [],
            PrestigeLevels = data.PrestigeLevels ?? [],
            Stats = data.Stats ?? new GameStats(),
            Achievements = data.Achievements?.ToHashSet() ?? [],
            ContractOffers = data.ContractOffers ?? [],
            ActiveContracts = data.ActiveContracts ?? [],
            ActiveJobs = data.ActiveJobs ?? [],
            Targets = data.Targets ?? [],
        };

        var elapsed = DateTimeOffset.UtcNow - data.SavedAtUtc;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;
        if (elapsed > OfflineCap)
            elapsed = OfflineCap;

        return (state, elapsed);
    }
}