using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Nullbyte.Core;

namespace Nullbyte.Scripting;

// ---------------------------------------------------------------------------
// ScriptInstance: a compiled script and its live state. The instance object
// is the script's "body" — output, error, data. Execution is a running Task
// suspended on awaits; it is never serialized (source restart model).
// ---------------------------------------------------------------------------

public sealed class ScriptInstance
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public ScriptData Data { get; internal set; } = new();

    public List<string> Output { get; } = new();
    public long OutputTotal { get; internal set; }
    public Exception? Error { get; internal set; }
    public bool IsStopRequested { get; internal set; }

    internal Task? Execution { get; set; }
    internal TaskCompletionSource Stopped { get; }
        = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int ApiCallsThisTick { get; set; }

    public bool IsRunning
        => !IsStopRequested && Error is null && Execution is { IsCompleted: false };

    internal void AddOutput(string message)
    {
        Output.Add($"[{Name}] {message}");
        OutputTotal++;
        if (Output.Count > 50)
            Output.RemoveAt(0);
    }
}

public sealed class ScriptCompileException : Exception
{
    public ScriptCompileException(string message) : base(message) { }
}

/// <summary>Internal: unwinds a script's await chain when stopped by the user.</summary>
internal sealed class ScriptStoppedException : Exception
{
    public ScriptStoppedException() : base("Script stopped by user.") { }
}

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

    private readonly GameSession _session;
    private readonly List<ScriptInstance> _scripts = new();

    private TaskCompletionSource _barrier = NewBarrier();

    private static TaskCompletionSource NewBarrier()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ScriptingSystem(GameSession session) { _session = session; }

    public IReadOnlyList<ScriptInstance> Scripts => _scripts;
    public ScriptInstance? Find(string name)
        => _scripts.FirstOrDefault(s => s.Name == name);

    // -- Compile & start ----------------------------------------------------

    public async Task<ScriptInstance> StartAsync(string name, string source,
                                                 ScriptData? data = null)
    {
        var options = ScriptOptions.Default
            .WithGlobalsType(typeof(ScriptGlobals))
            .WithUsings("System", "System.Collections.Generic", "System.Threading.Tasks")
            .WithAllowUnsafe(false)
            .WithEmitDebugInformation(false);
        // Production hardening (design doc Phase 4): pin References to
        // System.Runtime + this assembly, add a banned-pattern analyzer.
        // Scripts see no file/network/console access with this usings set.

        var compiled = CSharpScript.Create(source, options, typeof(ScriptGlobals));
        var diagnostics = compiled.Compile();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
            throw new ScriptCompileException(string.Join("\n", errors.Select(d => d.ToString())));

        var script = new ScriptInstance { Name = name, Source = source };
        if (data is not null)
            script.Data = data;
        var api = new NullbyteApi(this, _session, script);

        script.Execution = Task.Run(async () =>
        {
            try
            {
                await compiled.RunAsync(new ScriptGlobals(api));
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

        _scripts.Add(script);
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
        var old = _barrier;
        _barrier = NewBarrier();

        foreach (var s in _scripts)
            s.ApiCallsThisTick = 0;

        old.TrySetResult(); // all suspended scripts resume, run to next await

        // Stopped scripts are removed once their execution has unwound.
        _scripts.RemoveAll(s => s.IsStopRequested && (s.Execution?.IsCompleted ?? true));
    }

    // -- ns API internals ---------------------------------------------------

    /// <summary>
    /// Await point: resolves at the next Tick(), or throws to unwind if the
    /// user stopped the script while it was suspended.
    /// </summary>
    private async Task ThrowIfStopped(ScriptInstance script)
    {
        var completed = await Task.WhenAny(_barrier.Task, script.Stopped.Task);
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

// ---------------------------------------------------------------------------
// Persistence: scripts save as source + data + running flag. On load, running
// scripts are recompiled and restarted (they reconverge — the design-doc
// save model for Roslyn scripting).
// ---------------------------------------------------------------------------

public sealed class ScriptSaveEntry
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public ScriptData? Data { get; set; }
    public bool Running { get; set; }
}

public static class ScriptPersistence
{
    public const string Folder = "scripts";
    public const string StateFile = "scripts/scripts.json";

    public static string SourcePath(string name) => Path.Combine(Folder, name + ".cs");

    public static void Save(IEnumerable<ScriptInstance> scripts)
    {
        Directory.CreateDirectory(Folder);
        var entries = scripts.Select(s => new ScriptSaveEntry
        {
            Name = s.Name,
            Source = s.Source,
            Data = s.Data,
            Running = s.IsRunning,
        }).ToList();
        File.WriteAllText(StateFile, JsonSerializer.Serialize(entries));
    }

    public static List<ScriptSaveEntry> Load()
    {
        if (!File.Exists(StateFile))
            return new();
        try
        {
            return JsonSerializer.Deserialize<List<ScriptSaveEntry>>(
                       File.ReadAllText(StateFile)) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}