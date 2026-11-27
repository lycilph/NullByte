using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Scripting;

/// <summary>Implements the player's skills scripting API.</summary>
public sealed class NullByteSkillsApi : INullByteSkillsApi
{
    public NullByteSkillsApi(PlayerSkills skills, SkillFormulas skillFormulas)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(skillFormulas);

        Hacking = new NullByteSkillApi(skills, skillFormulas, SkillKind.Hacking);
        Strength = new NullByteSkillApi(skills, skillFormulas, SkillKind.Strength);
        Defense = new NullByteSkillApi(skills, skillFormulas, SkillKind.Defense);
        Dexterity = new NullByteSkillApi(skills, skillFormulas, SkillKind.Dexterity);
        Agility = new NullByteSkillApi(skills, skillFormulas, SkillKind.Agility);
        Charisma = new NullByteSkillApi(skills, skillFormulas, SkillKind.Charisma);
    }

    public INullByteSkillApi Hacking { get; }
    public INullByteSkillApi Strength { get; }
    public INullByteSkillApi Defense { get; }
    public INullByteSkillApi Dexterity { get; }
    public INullByteSkillApi Agility { get; }
    public INullByteSkillApi Charisma { get; }
}
