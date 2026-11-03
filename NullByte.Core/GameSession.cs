using System.Collections.Concurrent;
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
    private static readonly Lock gate = new();

    public const int TickMs = 100;

    public GameContent Content { get; }
    public GameState State { get; }

    private readonly ConcurrentQueue<IGameCommand> inbox = new();

    public GameSession(GameContent content, GameState? loadedState = null)
    {
        Content = content;

        if (loadedState == null)
        {
            State = new GameState();
            HackSystem.InitializeTargets(State, content);
        }
        else
            State = loadedState;
    }

    /// <summary>Queue a command; applied at the start of the next tick.</summary>
    public void Submit(IGameCommand command) => inbox.Enqueue(command);

    /// <summary>Advance exactly one tick. The only place state mutates.</summary>
    public void Tick(bool isFastforward = false)
    {
        lock (gate)
        {
            while (inbox.TryDequeue(out var cmd))
                cmd.Apply(State, Content);

            HackSystem.Tick(State, Content);
            EconomySystem.Tick(State);
            ProgressionSystem.Tick(State);
            AchievementsSystem.Tick(State, Content);

            if (!isFastforward)
            {
                AtmosphereSystem.Tick(State, Content);
                ContractsSystem.Tick(State, Content);
            }

            State.GameTick++;
        }
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

    /// <summary>Thread-safe read access for the scripting API and UI.
    /// Callback must not mutate state, must not await, must not call
    /// back into Submit/Tick.</summary>
    public T Read<T>(Func<GameState, T> read)
    {
        lock (gate) return read(State);
    }

    /// <summary>Thread-safe read access for capturing all data to save.</summary>
    public SaveData CaptureSaveData()
    {
        lock (gate)
        {
            return new SaveData
            {
                SchemaVersion = 1,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Money = State.Money,
                Xp = State.Xp,
                Level = State.Level,
                SkillPoints = State.SkillPoints,
                ZeroDays = State.ZeroDays,
                GameTick = State.GameTick,
                UpgradeLevels = new Dictionary<string, int>(State.UpgradeLevels),
                PerkLevels = new Dictionary<string, int>(State.PerkLevels),
                PrestigeLevels = new Dictionary<string, int>(State.PrestigeLevels),
                Stats = State.Stats,
                Achievements = [.. State.Achievements],
                ContractOffers = [.. State.ContractOffers],
                ActiveContracts = [.. State.ActiveContracts],
                ActiveJobs = [.. State.ActiveJobs],
                Targets = new Dictionary<string, TargetState>(State.Targets),
            };
        }
    }

}
