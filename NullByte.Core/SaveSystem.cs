using System.Text.Json;

namespace NullByte.Core;

// ---------------------------------------------------------------------------
// Save/load with schema versioning and offline elapsed-time capture.
// The caller turns offlineElapsed into FastForward ticks (capped here).
// ---------------------------------------------------------------------------

public sealed class SaveData
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset SavedAtUtc { get; set; }
    public double Money { get; set; }
    public double Xp { get; set; }
    public int Level { get; set; }
    public int SkillPoints { get; set; }
    public long GameTick { get; set; }
    public Dictionary<string, int> UpgradeLevels { get; set; } = [];
    public Dictionary<string, int> PerkLevels { get; set; } = [];
    public GameStats? Stats { get; set; }
    public List<string> Achievements { get; set; } = [];
    public List<ContractOfferState> ContractOffers { get; set; } = [];
    public List<ActiveContractState> ActiveContracts { get; set; } = [];
    public List<ActiveHack> ActiveHacks { get; set; } = [];
}


public static class SaveSystem
{
    public const string SaveFile = "save.json";
    public static readonly TimeSpan OfflineCap = TimeSpan.FromHours(8);

    private static readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
    };

    public static void Save(GameState state, string path = SaveFile)
    {
        var data = new SaveData
        {
            SchemaVersion = 1,
            SavedAtUtc = DateTimeOffset.UtcNow,
            Money = state.Money,
            Xp = state.Xp,
            Level = state.Level,
            SkillPoints = state.SkillPoints,
            GameTick = state.GameTick,
            UpgradeLevels = new Dictionary<string, int>(state.UpgradeLevels),
            PerkLevels = new Dictionary<string, int>(state.PerkLevels),
            Stats = state.Stats,
            Achievements = [.. state.Achievements],
            ContractOffers = [.. state.ContractOffers],
            ActiveContracts = [.. state.ActiveContracts],
            ActiveHacks = [.. state.ActiveHacks],
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, options));
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
            GameTick = data.GameTick,
            UpgradeLevels = data.UpgradeLevels ?? [],
            PerkLevels = data.PerkLevels ?? [],
            Stats = data.Stats ?? new GameStats(),
            Achievements = data.Achievements?.ToHashSet() ?? [],
            ContractOffers = data.ContractOffers ?? [],
            ActiveContracts = data.ActiveContracts ?? [],
            ActiveHacks = data.ActiveHacks ?? [],
        };

        var elapsed = DateTimeOffset.UtcNow - data.SavedAtUtc;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;
        if (elapsed > OfflineCap)
            elapsed = OfflineCap;

        return (state, elapsed);
    }
}