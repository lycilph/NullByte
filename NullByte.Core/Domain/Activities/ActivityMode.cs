namespace NullByte.Core.Domain.Activities;

/// <summary>
/// How much of the player's attention an activity gets.
/// </summary>
/// <remarks>
/// Core only cares about the reward: a <see cref="Background"/> activity earns
/// less (see <c>BalanceConfig.UnfocusedRewardFactor</c>). Whether the console is
/// blocked while focused is up to the host.
/// </remarks>
public enum ActivityMode
{
    /// <summary>The player is giving the activity their full attention: full rewards.</summary>
    Focused,

    /// <summary>The activity runs on the side: reduced rewards, costs unchanged.</summary>
    Background
}