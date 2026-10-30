using NullByte.Core;

namespace NullByte.Scripting;

// ---------------------------------------------------------------------------
// ScriptingSystem: owns all script instances and the tick barrier.
//
// The host calls Tick() once per game tick, right after session.Tick().
// Tick() releases the barrier, which resolves every pending script action —
// so all script execution is tick-aligned by construction.
//
// CPU quota: MaxApiCallsPerTick per script. Calls above the budget resolve
// one tick later (soft throttle — the script slows down instead of dying).
// Later this budget scales with rig CPU/RAM, making hardware matter for
// scripting.
// ---------------------------------------------------------------------------

public sealed class ScriptingSystem
{
    public const int MaxApiCallsPerTick = 50;

    private readonly GameSession session;
    private readonly List<ScriptInstance> scripts = [];

    public ScriptingSystem(GameSession session) { this.session = session; }

    // -- Host integration ----------------------------------------------------

    /// <summary>Call once per game tick, right after session.Tick().</summary>
    public void Tick()
    {
        //var old = _barrier;
        //_barrier = NewBarrier();

        //foreach (var s in _scripts)
        //    s.ApiCallsThisTick = 0;

        //old.TrySetResult(); // all suspended scripts resume, run to next await

        //// Stopped scripts are removed once their execution has unwound.
        //_scripts.RemoveAll(s => s.IsStopRequested && (s.Execution?.IsCompleted ?? true));
    }
}
