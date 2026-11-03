using NullByte.Core;
using NullByte.Core.Commands;
using NullByte.Core.Systems;

namespace NullByte.Scripting.ScriptingApi;

/// <summary>
/// Player-facing scripting API.
///
/// Queries are synchronous properties (plain reads of engine state).
/// Actions are awaitable Tasks: they submit the same ICommand the console
/// UI submits, and resolve on the game tick barrier — a script physically
/// cannot act between ticks, and can never bypass game rules.
/// </summary>

public sealed class NullByteApi
{
    private readonly ScriptingSystem system;
    private readonly GameSession session;
    private readonly ScriptInstance script;

    internal NullByteApi(ScriptingSystem system, GameSession session, ScriptInstance script)
    {
        this.system = system;
        this.session = session;
        this.script = script;
    }

    // -- Queries -------------------------------------------------------------

    public double Money => session.Read(s => s.Money);
    public int Level => session.Read(s => s.Level);
    public double Xp => session.Read(s => s.Xp);
    public int SkillPoints => session.Read(s => s.SkillPoints);
    public long ZeroDays => session.Read(s => s.ZeroDays);
    public long Ticks => session.Read(s => s.GameTick);
    public int ActiveJobs => session.Read(s => s.ActiveJobs.Count);
    public int MaxConcurrentJobs => session.Read(HackSystem.MaxConcurrentHacks);
    
    // -- Upgrade queries -----------------------------------------------------

    public int CpuLevel => session.Read(s => s.UpgradeLevels.GetValueOrDefault("cpu"));
    public int RamLevel => session.Read(s => s.UpgradeLevels.GetValueOrDefault("ram"));

    // -- List queries --------------------------------------------------------

    /// <summary>Get list of targets</summary>
    public List<Target> ScanNetwork() 
        => [.. session.Content.Targets.Select(t => new Target(t.Id, t.Name, t.MinLevel, t.RequiredCpu, t.RequiredRam))];

    // -- Mark state (Phase 4.5) ----------------------------------------------

    /// <summary>Live money pool on a mark. Unknown target id → 0.</summary>
    public double TargetMoney(string targetId)
        => session.Content.FindTarget(targetId) is { } t
         ? session.Read(s => s.Targets[t.Id].CurrentMoney) : 0;

    /// <summary>Current heat on a mark. Unknown target id → 0.</summary>
    public double TargetHeat(string targetId)
        => session.Content.FindTarget(targetId) is { } t
         ? session.Read(s => s.Targets[t.Id].Heat) : 0;

    /// <summary>Max drainable pool of a mark. Unknown target id → 0.</summary>
    public double TargetMax(string targetId)
        => session.Content.FindTarget(targetId) is { } t
            ? HackSystem.MaxMoneyOf(t) : 0;

    public int TargetHackDuration(string targetId)
        => session.Content.FindTarget(targetId) is { } t
         ? session.Read(s => HackSystem.JobDuration(t, s, JobKind.Hack)) : 0;

    public int TargetGrowDuration(string targetId)
        => session.Content.FindTarget(targetId) is { } t
         ? session.Read(s => HackSystem.JobDuration(t, s, JobKind.Grow)) : 0;

    public int TargetScrubDuration(string targetId)
        => session.Content.FindTarget(targetId) is { } t
         ? session.Read(s => HackSystem.JobDuration(t, s, JobKind.Scrub)) : 0;

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
