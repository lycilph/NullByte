using System.Collections.Concurrent;
using NullByte.Core.Commands;
using NullByte.Core.Domain;
using NullByte.Core.Snapshots;
using NullByte.Core.Systems;

namespace NullByte.Core;

// ---------------------------------------------------------------------------
// GameSession: owns the state and the tick. UI -> Submit(command); the host
// calls Tick() on a fixed 100 ms schedule. Deterministic: state changes only
// happen inside Tick, in queue order.
// ---------------------------------------------------------------------------

public sealed class GameSession
{
    private readonly Lock gate = new();
    private readonly GameState state;
    private readonly ConcurrentQueue<IGameCommand> inbox = new();

    public const int TickMs = 100;
    public GameContent Content { get; }

    public ConcurrentQueue<string> EventLog { get => state.EventLog; }

    public GameSession(GameContent content, GameState? loadedState = null)
    {
        Content = content;

        if (loadedState == null)
        {
            state = new GameState();
            HackSystem.InitializeTargets(state, content);
        }
        else
            state = loadedState;
    }

    /// <summary>Queue a command; applied at the start of the next tick.</summary>
    public void Submit(IGameCommand command) => inbox.Enqueue(command);

    /// <summary>Advance exactly one tick. The only place state mutates.</summary>
    public void Tick(bool isFastforward = false)
    {
        lock (gate)
        {
            while (inbox.TryDequeue(out var cmd))
                cmd.Apply(state, Content);

            HackSystem.Tick(state, Content);
            EconomySystem.Tick(state);
            ProgressionSystem.Tick(state);
            AchievementsSystem.Tick(state, Content);

            if (!isFastforward)
            {
                AtmosphereSystem.Tick(state, Content);
                ContractsSystem.Tick(state, Content);
            }

            state.GameTick++;
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

    /// <summary>Thread-safe read access for capturing all data to save.</summary>
    public SaveData CaptureSaveData()
    {
        lock (gate)
        {
            return new SaveData
            {
                SchemaVersion = 1,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Money = state.Money,
                Xp = state.Xp,
                Level = state.Level,
                SkillPoints = state.SkillPoints,
                ZeroDays = state.ZeroDays,
                GameTick = state.GameTick,
                UpgradeLevels = new Dictionary<string, int>(state.UpgradeLevels),
                PerkLevels = new Dictionary<string, int>(state.PerkLevels),
                PrestigeLevels = new Dictionary<string, int>(state.PrestigeLevels),
                Stats = state.Statistics,
                Achievements = [.. state.Achievements],
                ContractOffers = [.. state.ContractOffers],
                ActiveContracts = [.. state.ActiveContracts],
                ActiveJobs = [.. state.ActiveJobs],
                Targets = new Dictionary<string, TargetState>(state.Targets),
            };
        }
    }

    /// <summary>Thread-safe read access for simple state data.</summary>
    public StateSnapshot CaptureStateSnapshot()
    {
        lock (gate)
        {
            return new StateSnapshot(
                state.Money,
                state.Xp,
                state.Level,
                state.SkillPoints,
                state.ZeroDays,
                state.GameTick);
        }
}