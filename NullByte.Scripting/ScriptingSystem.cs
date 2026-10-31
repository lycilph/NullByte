using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
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

public sealed class ScriptingSystem(GameSession session)
{
    public const int MaxApiCallsPerTick = 50;

    private readonly GameSession session = session;
    private readonly List<ScriptInstance> scripts = [];

    private TaskCompletionSource barrier = NewBarrier();
    private static TaskCompletionSource NewBarrier()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<ScriptInstance> Scripts => scripts;
    public ScriptInstance? Find(string name)
        => scripts.FirstOrDefault(s => s.Name == name);

    // -- Compile & start ----------------------------------------------------

    public async Task<ScriptInstance> StartAsync(string name, string source, ScriptData? data = null)
    {
        var options = ScriptOptions.Default
            .WithImports("System", "System.Collections.Generic", "System.Threading.Tasks")
            .WithAllowUnsafe(false)
            .WithEmitDebugInformation(false);
        //// Production hardening (design doc Phase 4): pin References to
        //// System.Runtime + this assembly, add a banned-pattern analyzer.
        //// Scripts see no file/network/console access with this usings set.

        var compiled = CSharpScript.Create(source, options, typeof(NullbyteApi));
        var diagnostics = compiled.Compile();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
            throw new ScriptCompileException(string.Join("\n", errors.Select(d => d.ToString())));

        var script = new ScriptInstance { Name = name, Source = source };
        if (data is not null)
            script.Data = data;
        var api = new NullbyteApi(this, session, script);

        script.Execution = Task.Run(async () =>
        {
            try
            {
                await compiled.RunAsync(api);
                script.AddOutput("...finished.");
            }
            catch (ScriptStoppedException)
            {
                script.AddOutput("...stopped.");
            }
            catch (Exception ex)
            {
                script.Error = ex;
                script.AddOutput($"ERROR: {ex.Message}");
            }
        });

        scripts.Add(script);
        return script;
    }

    public void Stop(string name)
    {
        var script = Find(name);
        if (script is null)
            return;
        script.IsStopRequested = true;
        script.Stopped.TrySetResult();
    }

    // -- Host integration ----------------------------------------------------

    /// <summary>Call once per game tick, right after session.Tick().</summary>
    public void Tick()
    {
        var old = barrier;
        barrier = NewBarrier();

        foreach (var s in scripts)
            s.ApiCallsThisTick = 0;

        old.TrySetResult(); // all suspended scripts resume, run to next await

        // Stopped scripts are removed once their execution has unwound.
        scripts.RemoveAll(s => s.IsStopRequested && (s.Execution?.IsCompleted ?? true));
    }

    // -- API internals -------------------------------------------------------

    /// <summary>
    /// Await point: resolves at the next Tick(), or throws to unwind if the
    /// user stopped the script while it was suspended.
    /// </summary>
    private async Task ThrowIfStopped(ScriptInstance script)
    {
        var completed = await Task.WhenAny(barrier.Task, script.Stopped.Task);
        if (completed == script.Stopped.Task)
            throw new ScriptStoppedException();
    }

    internal async Task Action(ScriptInstance script, Action submit)
    {
        script.ApiCallsThisTick++;
        if (script.ApiCallsThisTick > MaxApiCallsPerTick)
            await ThrowIfStopped(script); // over quota: cool down one tick

        submit();                          // queue the engine command
        await ThrowIfStopped(script);      // resolve at the next tick boundary
    }

    internal async Task Delay(ScriptInstance script, int ticks)
    {
        for (int i = 0; i < ticks; i++)
            await ThrowIfStopped(script);
    }
}
