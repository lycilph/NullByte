using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Nullbyte.Core;

// ---------------------------------------------------------------------------
// Commands: the ONLY way anything outside the engine changes game state.
// The console UI submits them today; the Roslyn scripting system will
// submit the same commands in Phase 4.
// ---------------------------------------------------------------------------

public interface ICommand
{
    void Apply(GameState state, GameContent content);
}

public sealed record StartHackCommand(string TargetId) : ICommand
{
    public void Apply(GameState state, GameContent content)
        => HackSystem.StartHack(state, content, TargetId);
}

public sealed record GrowTargetCommand(string TargetId) : ICommand
{
    public void Apply(GameState state, GameContent content)
        => HackSystem.StartGrow(state, content, TargetId);
}

public sealed record ScrubTargetCommand(string TargetId) : ICommand
{
    public void Apply(GameState state, GameContent content)
        => HackSystem.StartScrub(state, content, TargetId);
}

public sealed record BuyUpgradeCommand(string UpgradeId) : ICommand
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
        state.Stats.UpgradesBought++;
        state.Log($"Bought {def.Name} level {owned + 1} for ${cost:0.##}.");
    }
}

public sealed record SpendSkillCommand(string PerkId) : ICommand
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

public sealed record AcceptContractCommand(string ContractId) : ICommand
{
    public void Apply(GameState state, GameContent content)
        => ContractsSystem.Accept(state, content, ContractId);
}

public sealed record PrestigeCommand : ICommand
{
    public void Apply(GameState state, GameContent content)
        => PrestigeSystem.Reset(state, content);
}

public sealed record ImplantCommand(string ImplantId) : ICommand
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

        long cost = (long)Math.Ceiling(def.BaseCost * Math.Pow(def.GrowthRate, owned));
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

    private readonly ConcurrentQueue<ICommand> _inbox = new();

    public GameSession(GameContent content, GameState? loadedState = null)
    {
        Content = content;
        State = loadedState ?? new GameState();
    }

    /// <summary>Queue a command; applied at the start of the next tick.</summary>
    public void Submit(ICommand command) => _inbox.Enqueue(command);

    /// <summary>Advance exactly one tick. The only place state mutates.</summary>
    public void Tick()
    {
        while (_inbox.TryDequeue(out var cmd))
            cmd.Apply(State, Content);

        HackSystem.Tick(State, Content);
        EconomySystem.Tick(State);
        ProgressionSystem.Tick(State);
        AtmosphereSystem.Tick(State, Content);
        AchievementsSystem.Tick(State, Content);
        ContractsSystem.Tick(State, Content);

        State.GameTick++;
    }

    /// <summary>
    /// Offline catch-up: run N ticks without host pacing. Because all logic
    /// is in Tick(), "later" is just "more ticks" — no special offline code.
    /// </summary>
    public void FastForward(long ticks)
    {
        for (long i = 0; i < ticks; i++)
            Tick();
    }
}