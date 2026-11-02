using NullByte.Core;
using NullByte.Core.Commands;
using NullByte.Core.Systems;

namespace NullByte.Scripting;

/// <summary>
/// Player-facing scripting API.
///
/// Queries are synchronous properties (plain reads of engine state).
/// Actions are awaitable Tasks: they submit the same ICommand the console
/// UI submits, and resolve on the game tick barrier — a script physically
/// cannot act between ticks, and can never bypass game rules.
/// </summary>

public sealed class NullbyteApi
{
    private readonly ScriptingSystem system;
    private readonly GameSession session;
    private readonly ScriptInstance script;

    internal NullbyteApi(ScriptingSystem system, GameSession session, ScriptInstance script)
    {
        this.system = system;
        this.session = session;
        this.script = script;
    }

    // -- Queries -------------------------------------------------------------

    public double Money => session.State.Money;
    public double Level => session.State.Level;
    public double Xp => session.State.Xp;
    public double SkillPoints => session.State.SkillPoints;
    public double ZeroDays => session.State.ZeroDays;
    public long Ticks => session.State.GameTick;
    public int ActiveJobs => session.State.ActiveJobs.Count;
    public int MaxConcurrentJobs => HackSystem.MaxConcurrentHacks(session.State);

    // -- Mark state (Phase 4.5) ----------------------------------------------

    /// <summary>Live money pool on a mark. Unknown target id → 0.</summary>
    public double TargetMoney(string targetId)
        => session.Content.FindTarget(targetId) is { } t
            ? HackSystem.StateFor(session.State, t).CurrentMoney : 0;

    /// <summary>Current heat on a mark. Unknown target id → 0.</summary>
    public double TargetHeat(string targetId)
        => session.Content.FindTarget(targetId) is { } t
            ? HackSystem.StateFor(session.State, t).Heat : 0;

    /// <summary>Max drainable pool of a mark. Unknown target id → 0.</summary>
    public double TargetMax(string targetId)
        => session.Content.FindTarget(targetId) is { } t
            ? HackSystem.MaxMoneyOf(t) : 0;

    // -- Actions (emit engine commands; resolve at the next tick) ----------

    public Task Hack(string targetId)
        => system.Action(script, () => session.Submit(new StartHackCommand(targetId)));
    public Task Grow(string targetId)
        => system.Action(script, () => session.Submit(new GrowTargetCommand(targetId)));

    public Task Scrub(string targetId)
        => system.Action(script, () => session.Submit(new ScrubTargetCommand(targetId)));

    public Task Buy(string upgradeId)
        => system.Action(script, () => session.Submit(new BuyUpgradeCommand(upgradeId)));

    public Task Skill(string perkId)
        => system.Action(script, () => session.Submit(new SpendSkillCommand(perkId)));

    public Task Accept(string contractId)
        => system.Action(script, () => session.Submit(new AcceptContractCommand(contractId)));

    public Task Prestige()
        => system.Action(script, () => session.Submit(new PrestigeCommand()));

    // -- Time ---------------------------------------------------------------

    /// <summary>Suspend the script for N game ticks.</summary>
    public Task Wait(int ticks) => system.Delay(script, ticks);

    // -- Script console & persistent state ----------------------------------

    public void Log(string message) => script.AddOutput(message);

    /// <summary>
    /// Script-owned persistent state. Saved with the script source and
    /// restored on restart — the ONLY script state that survives save/load,
    /// because running continuations cannot be serialized (scripts restart
    /// and reconverge; keep durable facts here, not in locals).
    /// </summary>
    public ScriptData Data => script.Data;
}
