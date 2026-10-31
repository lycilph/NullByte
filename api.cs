using System.Collections.Generic;
using System.Threading.Tasks;
using Nullbyte.Core;

namespace Nullbyte.Scripting;

/// <summary>
/// The globals object passed to every script. Scripts reference it as `ns`.
/// </summary>
public sealed class ScriptGlobals
{
    public NullbyteApi ns { get; }

    internal ScriptGlobals(NullbyteApi api) { ns = api; }
}

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
    private readonly ScriptingSystem _system;
    private readonly GameSession _session;
    private readonly ScriptInstance _script;

    internal NullbyteApi(ScriptingSystem system, GameSession session, ScriptInstance script)
    {
        _system = system;
        _session = session;
        _script = script;
    }

    // -- Queries -----------------------------------------------------------

    public double Money => _session.State.Money;
    public double Level => _session.State.Level;
    public double Xp => _session.State.Xp;
    public double SkillPoints => _session.State.SkillPoints;
    public double ZeroDays => _session.State.ZeroDays;
    public long Ticks => _session.State.GameTick;
    public int ActiveHacks => _session.State.ActiveHacks.Count;

    // -- Actions (emit engine commands; resolve at the next tick) ----------

    public Task Hack(string targetId)
        => _system.Action(_script, () => _session.Submit(new StartHackCommand(targetId)));

    public Task Buy(string upgradeId)
        => _system.Action(_script, () => _session.Submit(new BuyUpgradeCommand(upgradeId)));

    public Task Skill(string perkId)
        => _system.Action(_script, () => _session.Submit(new SpendSkillCommand(perkId)));

    public Task Accept(string contractId)
        => _system.Action(_script, () => _session.Submit(new AcceptContractCommand(contractId)));

    public Task Prestige()
        => _system.Action(_script, () => _session.Submit(new PrestigeCommand()));

    // -- Time --------------------------------------------------------------

    /// <summary>Suspend the script for N game ticks.</summary>
    public Task Wait(int ticks) => _system.Delay(_script, ticks);

    // -- Script console & persistent state ----------------------------------

    public void Log(string message) => _script.AddOutput(message);

    /// <summary>
    /// Script-owned persistent state. Saved with the script source and
    /// restored on restart — the ONLY script state that survives save/load,
    /// because running continuations cannot be serialized (scripts restart
    /// and reconverge; keep durable facts here, not in locals).
    /// </summary>
    public ScriptData Data => _script.Data;
}

/// <summary>Key-value store for durable script state. Serialized as JSON.</summary>
public sealed class ScriptData
{
    public Dictionary<string, double> Numbers { get; set; } = new();
    public Dictionary<string, string> Strings { get; set; } = new();

    public double GetNum(string key, double def = 0)
        => Numbers.TryGetValue(key, out var v) ? v : def;
    public void SetNum(string key, double v) => Numbers[key] = v;

    public string GetStr(string key, string def = "")
        => Strings.TryGetValue(key, out var v) ? v : def;
    public void SetStr(string key, string v) => Strings[key] = v;
}