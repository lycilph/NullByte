namespace NullByte.Core.Domain.Activities;

/// <summary>
/// The kinds of place where a player can take courses.
/// </summary>
public enum FacilityKind
{
    /// <summary>Teaches hacking and charisma (the <c>study</c> command).</summary>
    University,

    /// <summary>Trains the combat skills (the <c>train</c> command).</summary>
    Gym
}