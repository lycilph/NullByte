using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Scripting;

/// <summary>Implements player information exposed through <c>nb.Player</c>.</summary>
public sealed class NullBytePlayerApi : INullBytePlayerApi
{
    private readonly Player player;

    public NullBytePlayerApi(Player player, SkillFormulas skillFormulas)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        Skills = new SkillsInfo(player.Skills, skillFormulas);
    }

    public double Money => player.Money;
    public SkillsInfo Skills { get; }
}
