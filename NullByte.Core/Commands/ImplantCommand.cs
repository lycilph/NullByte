using NullByte.ConsoleUI.Systems;

namespace NullByte.Core.Commands;

// ---------------------------------------------------------------------------
// Commands: the ONLY way anything outside the engine changes game state.
// The console UI submits them today; the Roslyn scripting system will
// submit the same commands in Phase 4.
// ---------------------------------------------------------------------------

public sealed record ImplantCommand(string ImplantId) : IGameCommand
{
    public void Apply(GameState state, GameContent content)
    {
        var def = content.FindImplant(ImplantId);
        if (def is null)
        {
            state.Log($"Unknown implant '{ImplantId}'.");
            return;
        }

        int owned = state.PrestigeLevels.GetValueOrDefault(def.Id);
        if (owned >= def.MaxLevel)
        {
            state.Log($"{def.Name} is already maxed.");
            return;
        }

        long cost = CostMath.ImplantCost(def, owned);
        if (state.ZeroDays < cost)
        {
            state.Log($"Not enough 0-days for {def.Name} ({cost} needed).");
            return;
        }

        state.ZeroDays -= cost;
        state.PrestigeLevels[def.Id] = owned + 1;
        state.Log($"Installed {def.Name} level {owned + 1} for {cost} 0-days.");
    }
}