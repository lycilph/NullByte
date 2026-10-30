using NullByte.Core.Systems;

namespace NullByte.Core.Commands;

// ---------------------------------------------------------------------------
// Commands: the ONLY way anything outside the engine changes game state.
// The console UI submits them today; the Roslyn scripting system will
// submit the same commands in Phase 4.
// ---------------------------------------------------------------------------

public sealed record PrestigeCommand : IGameCommand
{
    public void Apply(GameState state, GameContent content)
        => PrestigeSystem.Reset(state, content);
}