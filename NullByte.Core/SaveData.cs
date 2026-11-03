namespace NullByte.Core;

public sealed class SaveData
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset SavedAtUtc { get; set; }
    public double Money { get; set; }
    public double Xp { get; set; }
    public int Level { get; set; }
    public int SkillPoints { get; set; }
    public long ZeroDays { get; set; }
    public long GameTick { get; set; }
    public Dictionary<string, int> UpgradeLevels { get; set; } = [];
    public Dictionary<string, int> PerkLevels { get; set; } = [];
    public Dictionary<string, int> PrestigeLevels { get; set; } = [];
    public GameStats? Stats { get; set; }
    public List<string> Achievements { get; set; } = [];
    public List<ContractOfferState> ContractOffers { get; set; } = [];
    public List<ActiveContractState> ActiveContracts { get; set; } = [];
    public List<ActiveJob> ActiveJobs { get; set; } = [];
    public Dictionary<string, TargetState> Targets { get; set; } = [];
}
