namespace NullByte.Core.Snapshots;

public sealed record StateSnapshot(
    double Money,
    double Xp,
    int Level,
    int SkillPoints,
    long ZeroDays,
    long GameTick);