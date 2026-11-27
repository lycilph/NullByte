using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Scripting;

/// <summary>Implements the player's skills scripting info.</summary>
public sealed class SkillsInfo
{
    public SkillsInfo(PlayerSkills skills, SkillFormulas skillFormulas)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(skillFormulas);

        Hacking = new SkillInfo(skills, skillFormulas, SkillKind.Hacking);
        Strength = new SkillInfo(skills, skillFormulas, SkillKind.Strength);
        Defense = new SkillInfo(skills, skillFormulas, SkillKind.Defense);
        Dexterity = new SkillInfo(skills, skillFormulas, SkillKind.Dexterity);
        Agility = new SkillInfo(skills, skillFormulas, SkillKind.Agility);
        Charisma = new SkillInfo(skills, skillFormulas, SkillKind.Charisma);
    }

    public SkillInfo Hacking { get; }
    public SkillInfo Strength { get; }
    public SkillInfo Defense { get; }
    public SkillInfo Dexterity { get; }
    public SkillInfo Agility { get; }
    public SkillInfo Charisma { get; }
}
