namespace NullByte.Core.Domain;

/// <summary>
/// The skills a player can train. Each skill has its own experience and a
/// level derived from it (see <see cref="Formulas.SkillFormulas"/>).
/// </summary>
/// <remarks>
/// The numeric values are used as array indexes by <see cref="PlayerSkills"/>,
/// so they must stay contiguous and start at zero. They are not saved: saves
/// use the names.
/// </remarks>
public enum SkillKind
{
    Hacking = 0,
    Strength = 1,
    Defense = 2,
    Dexterity = 3,
    Agility = 4,
    Charisma = 5
}