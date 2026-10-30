using System.Collections.Concurrent;
using System.Windows.Input;
using NullByte.Core.Commands;
using NullByte.Core.Systems;

namespace NullByte.Core;

// ---------------------------------------------------------------------------
// GameSession: owns the state and the tick. UI -> Submit(command); the host
// calls Tick() on a fixed 100 ms schedule. Deterministic: state changes only
// happen inside Tick, in queue order.
// ---------------------------------------------------------------------------

public sealed class GameSession
{
    public const int TickMs = 100;

    public GameContent Content { get; }
    public GameState State { get; }

    private readonly ConcurrentQueue<IGameCommand> inbox = new();

    public GameSession(GameContent content, GameState? loadedState = null)
    {
        Content = content;
        State = loadedState ?? new GameState();
    }

    /// <summary>Queue a command; applied at the start of the next tick.</summary>
    public void Submit(IGameCommand command) => inbox.Enqueue(command);

    /// <summary>Advance exactly one tick. The only place state mutates.</summary>
    public void Tick(bool isFastforward = false)
    {
        while (inbox.TryDequeue(out var cmd))
            cmd.Apply(State, Content);
        
        HackSystem.Tick(State, Content);
        EconomySystem.Tick(State);
        ProgressionSystem.Tick(State);

        if (!isFastforward)
            AtmosphereSystem.Tick(State, Content);

        State.GameTick++;
    }

    /// <summary>
    /// Offline catch-up: run N ticks without host pacing. Because all logic
    /// is in Tick(), "later" is just "more ticks" — no special offline code.
    /// </summary>
    public void FastForward(long ticks)
    {
        for (long i = 0; i < ticks; i++)
            Tick(true);
    }
}
