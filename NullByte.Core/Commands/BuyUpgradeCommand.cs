using NullByte.ConsoleUI.Systems;

namespace NullByte.Core.Commands;

// ---------------------------------------------------------------------------
// Commands: the ONLY way anything outside the engine changes game state.
// The console UI submits them today; the Roslyn scripting system will
// submit the same commands in Phase 4.
// ---------------------------------------------------------------------------

public sealed record BuyUpgradeCommand(string UpgradeId) : IGameCommand
{
    public void Apply(GameState state, GameContent content)
    {
        var def = content.FindUpgrade(UpgradeId);
        if (def is null)
        {
            state.Log($"Unknown upgrade '{UpgradeId}'.");
            return;
        }

        int owned = state.UpgradeLevels.GetValueOrDefault(def.Id);
        if (owned >= def.MaxLevel)
        {
            state.Log($"{def.Name} is already maxed.");
            return;
        }

        double cost = CostMath.UpgradeCost(def, owned);
        if (state.Money < cost)
        {
            state.Log($"Not enough money for {def.Name} (${cost:0.##} needed).");
            return;
        }

        state.Money -= cost;
        state.UpgradeLevels[def.Id] = owned + 1;
        state.Log($"Bought {def.Name} level {owned + 1} for ${cost:0.##}.");
    }
}