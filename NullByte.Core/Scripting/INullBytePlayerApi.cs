namespace NullByte.Core.Scripting;

/// <summary>Defines player information exposed through <c>nb.Player</c>.</summary>
public interface INullBytePlayerApi
{
    /// <summary>Gets the player's current money.</summary>
    double Money { get; }

    /// <summary>Gets the player's skills.</summary>
    SkillsInfo Skills { get; }
}
