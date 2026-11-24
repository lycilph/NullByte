namespace NullByte.Core.Scripting.Activities;

/// <summary>
/// What the player is doing right now, as seen by a script.
/// </summary>
/// <param name="Kind">One of <c>study</c>, <c>train</c>, <c>work</c> or <c>crime</c>.</param>
/// <param name="Description">A short description, e.g. <c>Studying algorithms at rothman-university</c>.</param>
/// <param name="InBackground">Whether the activity runs in the background (with reduced rewards) rather than being focused on.</param>
/// <param name="Elapsed">How long it has been going, in game time.</param>
/// <param name="NetMoney">Money gained so far, net of costs: negative if it has cost more than it paid.</param>
/// <param name="Experience">Experience gained so far, by lowercase skill name.</param>
/// <param name="Reputation">Reputation gained so far.</param>
/// <param name="Attempts">Attempts made so far (crimes).</param>
/// <param name="Successes">Attempts that succeeded so far (crimes).</param>
public sealed record ActivityStatus(
    string Kind,
    string Description,
    bool InBackground,
    TimeSpan Elapsed,
    double NetMoney,
    IReadOnlyDictionary<string, double> Experience,
    double Reputation,
    int Attempts,
    int Successes);