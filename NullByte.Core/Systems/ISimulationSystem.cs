using NullByte.Core.Simulation;

namespace NullByte.Core.Systems;

/// <summary>
/// A piece of game logic that advances with game time on every iteration
/// of the <see cref="GameLoop"/>.
/// </summary>
/// <remarks>
/// <para>
/// Systems run on the game thread, so they can read and write game state
/// without locking. They must be quick and must never block: anything that
/// takes game time to finish (such as a hack) belongs in the scheduler, not
/// in a system.
/// </para>
/// <para>
/// A system should be a thin driver. State lives in the domain model and the
/// numbers live in the formulas, so a system can be tested by calling
/// <see cref="Update"/> directly with a chosen delta, without a running loop.
/// </para>
/// </remarks>
public interface ISimulationSystem
{
    /// <summary>
    /// Advances the system by the given amount of game time.
    /// </summary>
    /// <param name="delta">
    /// The game time that has passed since the previous update. Always
    /// positive when called by the game loop.
    /// </param>
    void Update(TimeSpan delta);
}