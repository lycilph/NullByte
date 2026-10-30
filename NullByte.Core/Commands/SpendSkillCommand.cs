namespace NullByte.Core.Commands;

public sealed record SpendSkillCommand(string PerkId) : IGameCommand
{
    public void Apply(GameState state, GameContent content)
    {
        var def = content.FindPerk(PerkId);
        if (def is null)
        {
            state.Log($"Unknown perk '{PerkId}'.");
            return;
        }

        int owned = state.PerkLevels.GetValueOrDefault(def.Id);
        if (owned >= def.MaxLevel)
        {
            state.Log($"{def.Name} is already maxed.");
            return;
        }
        if (state.SkillPoints < 1)
        {
            state.Log("No skill points — level up to earn more.");
            return;
        }

        state.SkillPoints--;
        state.PerkLevels[def.Id] = owned + 1;
        state.Log($"Trained {def.Name} level {owned + 1}.");
    }
}