namespace NullByte.Core.Scripting;

/// <summary>Defines the information exposed for one player skill.</summary>
public interface INullByteSkillApi
{
    /// <summary>Gets the accumulated experience for this skill.</summary>
    double Experience { get; }

    /// <summary>Gets the current level for this skill.</summary>
    int Level { get; }
}
