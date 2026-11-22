namespace NullByte.Core.Domain;

/// <summary>
/// A crime: it takes a fixed time, succeeds or fails by a roll, and pays on success.
/// </summary>
/// <param name="Name">Unique command-friendly name, e.g. <c>shoplift</c>.</param>
/// <param name="DurationSeconds">Time one attempt takes.</param>
/// <param name="Money">Money earned on success.</param>
/// <param name="Difficulty">How hard it is; a higher value lowers the success chance.</param>
/// <param name="Experience">Experience gained on success, per skill.</param>
/// <param name="SuccessWeights">How much each skill counts towards the success chance.</param>
public sealed record Crime(
    string Name,
    double DurationSeconds,
    double Money,
    double Difficulty,
    IReadOnlyDictionary<SkillKind, double> Experience,
    IReadOnlyDictionary<SkillKind, double> SuccessWeights);