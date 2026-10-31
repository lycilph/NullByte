using Microsoft.CodeAnalysis.Scripting;
using NullByte.Core;
using NullByte.Core.Commands;

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
    public int ActiveHacks => session.State.ActiveHacks.Count;

    // -- Actions (emit engine commands; resolve at the next tick) ----------

    public Task Hack(string targetId)
        => system.Action(script, () => session.Submit(new StartHackCommand(targetId)));

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
