namespace NullByte.Core.Scripting;

/// <summary>Defines the player's skills exposed through <c>nb.Player.Skills</c>.</summary>
public interface INullByteSkillsApi
{
    INullByteSkillApi Hacking { get; }
    INullByteSkillApi Strength { get; }
    INullByteSkillApi Defense { get; }
    INullByteSkillApi Dexterity { get; }
    INullByteSkillApi Agility { get; }
    INullByteSkillApi Charisma { get; }
}
