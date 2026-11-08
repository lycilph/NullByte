using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Simulation;

namespace NullByte.Core.Systems;

/// <summary>
/// Pays the player the botnet's passive income as game time passes.
/// </summary>
/// <remarks>
/// Runs on the game thread, driven by <see cref="GameLoop"/>. Because the
/// botnet is only ever changed on that same thread, the production rate is
/// constant for the length of one update: upgrades take effect from the next
/// update on.
/// </remarks>
public sealed class BotnetSystem : ISimulationSystem
{
    private readonly GameState gameState;
    private readonly BotnetFormulas formulas;

    public BotnetSystem(GameState gameState, BotnetFormulas formulas)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));

        this.formulas = formulas
            ?? throw new ArgumentNullException(nameof(formulas));
    }

    /// <inheritdoc />
    public void Update(TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero)
            return;

        var botnet = gameState.Botnet;

        if (botnet.NodeCount == 0)
            return;

        var earned = formulas.CalculateTotalProduction(botnet) * delta.TotalSeconds;

        if (earned <= 0)
            return;

        gameState.Player.AddMoney(earned);
        botnet.RecordEarnings(earned);
    }
}