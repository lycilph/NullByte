using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Scripting;

/// <summary>Implements the scripting API for one player skill.</summary>
public sealed class NullByteSkillApi : INullByteSkillApi
{
    private readonly PlayerSkills skills;
    private readonly SkillFormulas skillFormulas;
    private readonly SkillKind kind;

    public NullByteSkillApi(PlayerSkills skills, SkillFormulas skillFormulas, SkillKind kind)
    {
        this.skills = skills ?? throw new ArgumentNullException(nameof(skills));
        this.skillFormulas = skillFormulas ?? throw new ArgumentNullException(nameof(skillFormulas));
        this.kind = kind;
    }

    public double Experience => skills.GetExperience(kind);
    public int Level => skillFormulas.CalculateLevel(kind, Experience);
}
