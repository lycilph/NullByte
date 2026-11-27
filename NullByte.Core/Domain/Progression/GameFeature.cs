namespace NullByte.Core.Domain.Progression;

/// <summary>
/// A part of the game that the player's journey unlocks. Features are coarse on purpose:
/// one feature usually stands for a handful of related commands and script API calls.
/// </summary>
/// <remarks>
/// Commands that are not tied to a feature (help, status, saving, ...) are always available.
/// See STORY.md for the story reason behind every feature.
/// </remarks>
public enum GameFeature
{
    /// <summary>Discover neighbouring servers (<c>scan</c>).</summary>
    Scan,

    /// <summary>Move between servers and inspect them (<c>connect</c>, <c>home</c>, <c>analyze</c>).</summary>
    Connect,

    /// <summary>Take root access to a server (<c>nuke</c>).</summary>
    Nuke,

    /// <summary>Steal money from a server (<c>hack</c>).</summary>
    Hack,

    /// <summary>Reduce a server's security (<c>weaken</c>).</summary>
    Weaken,

    /// <summary>Increase the money on a server (<c>grow</c>).</summary>
    Grow,

    /// <summary>The virtual file system (<c>ls</c>, <c>cat</c>, <c>cd</c>, <c>mkdir</c>, ...).</summary>
    Files,

    /// <summary>Writing and running scripts (<c>edit</c>, <c>run</c>, <c>ps</c>, <c>kill</c>, <c>scp</c>, ...).</summary>
    Scripts,

    /// <summary>Passive income from copies of yourself (<c>botnet</c>).</summary>
    Botnet,

    /// <summary>Studying and training to improve skills (<c>study</c>, <c>train</c>, <c>skills</c>).</summary>
    Learning,

    /// <summary>Holding a job (<c>work</c>).</summary>
    Work,

    /// <summary>Committing crimes (<c>crime</c>).</summary>
    Crime,

    /// <summary>Planting permanent back doors (<c>backdoor</c>).</summary>
    Backdoor,

    /// <summary>Renting cloud servers (<c>cloud</c>).</summary>
    Cloud
}
