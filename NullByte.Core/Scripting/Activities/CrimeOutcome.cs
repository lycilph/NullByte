namespace NullByte.Core.Scripting.Activities;

/// <summary>
/// How one attempt at a crime went, as seen by a script.
/// </summary>
/// <param name="Started">Whether the attempt could be started. If not, <see cref="Message"/> says why and nothing else is meaningful.</param>
/// <param name="Completed">Whether the attempt ran to its end. It does not if the player stopped it first.</param>
/// <param name="Succeeded">Whether the attempt succeeded.</param>
/// <param name="Chance">The chance the attempt had of succeeding.</param>
/// <param name="Money">Money earned (zero for a failure).</param>
/// <param name="Experience">Experience gained, by lowercase skill name.</param>
/// <param name="Message">A short description of what happened.</param>
public sealed record CrimeOutcome(
    bool Started,
    bool Completed,
    bool Succeeded,
    double Chance,
    double Money,
    IReadOnlyDictionary<string, double> Experience,
    string Message);