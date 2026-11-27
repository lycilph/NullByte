using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Scripting;

/// <summary>Implements the info for one player skill.</summary>
public sealed class SkillInfo
{
    private readonly PlayerSkills skills;
    private readonly SkillFormulas skillFormulas;
    private readonly SkillKind kind;

    public SkillInfo(PlayerSkills skills, SkillFormulas skillFormulas, SkillKind kind)
    {
        this.skills = skills ?? throw new ArgumentNullException(nameof(skills));
        this.skillFormulas = skillFormulas ?? throw new ArgumentNullException(nameof(skillFormulas));
        this.kind = kind;
    }

    public double Experience => skills.GetExperience(kind);
    public int Level => skillFormulas.CalculateLevel(kind, Experience);
}
