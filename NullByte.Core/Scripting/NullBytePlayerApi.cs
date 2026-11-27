using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Scripting;

/// <summary>Implements player information exposed through <c>nb.Player</c>.</summary>
public sealed class NullBytePlayerApi : INullBytePlayerApi
{
    private readonly Player player;
    private readonly NarrativeState? narrative;

    public NullBytePlayerApi(Player player, SkillFormulas skillFormulas, NarrativeState? narrative = null)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.narrative = narrative;
        skills = new SkillsInfo(player.Skills, skillFormulas);
    }

    public double Money { get { narrative?.EnsureCapability("player.profile"); return player.Money; } }
    public SkillsInfo Skills { get { narrative?.EnsureCapability("player.inspect"); return skills; } }
    private readonly SkillsInfo skills;
}
