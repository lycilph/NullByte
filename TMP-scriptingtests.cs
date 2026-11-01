using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nullbyte.Core;
using Nullbyte.Scripting;
using Xunit;

namespace Nullbyte.Scripting.Tests;

// ---------------------------------------------------------------------------
// Scripting tests: real Roslyn compiles + real engine, manual ticks.
//
// Harness rules (same philosophy as EngineTests, plus async realities):
//  - No wall-clock timing assertions: the barrier is released by Tick(),
//    so "time" is just the number of tick() calls. Delays only exist to let
//    thread-pool continuations run between ticks.
//  - Race tolerance: StartAsync's Task.Run may not have reached the script's
//    first await before the first Tick() — so a script can miss one barrier
//    release. Tests therefore assert bounded conditions (>= n) or poll with
//    RunUntilAsync, never exact tick counts.
// ---------------------------------------------------------------------------

public class ScriptingTests
{
    private static GameSession NewSession() => new(ContentLoader.Load());

    /// <summary>One game tick + one barrier release, with a pause for continuations.</summary>
    private static async Task TickAsync(GameSession session, ScriptingSystem scripting)
    {
        session.Tick();
        scripting.Tick();
        await Task.Delay(20); // continuations run on the thread pool
    }

    /// <summary>Tick until the condition holds, or maxTicks run out.</summary>
    private static async Task<bool> RunUntilAsync(GameSession session, ScriptingSystem scripting,
                                                   Func<bool> done, int maxTicks = 300)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < maxTicks && sw.Elapsed.TotalSeconds < 10; i++)
        {
            await TickAsync(session, scripting);
            if (done())
                return true;
        }
        return done();
    }

    // -- Compile-time lockdown ------------------------------------------------

    [Fact]
    public async Task Compile_UnknownApiMethod_ThrowsWithDiagnostics()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("bad", "ns.UndefinedMethod();"));

        Assert.Contains("CS1061", ex.Message); // '... does not contain a definition for ...'
        Assert.Empty(scripting.Scripts);       // failed compiles never register
    }

    [Fact]
    public async Task Compile_BannedUsing_Throws()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        // WithUsings alone does NOT block this (it only sets default imports,
        // a script's own using is legal syntax) — ScriptGuard must.
        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("io", "using System.IO;\nFile.Exists(\"x\");"));

        Assert.Contains("BANNED", ex.Message);
        Assert.Empty(scripting.Scripts);
    }

    [Fact]
    public async Task Compile_FullyQualifiedBannedType_Throws()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        // No using directive at all — the identifier screen must still catch it.
        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("sneaky", "System.IO.File.Exists(\"x\");"));

        Assert.Contains("File", ex.Message);
        Assert.Empty(scripting.Scripts);
    }

    [Fact]
    public async Task Compile_HashRDirective_Throws()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        // Roslyn scripting honors '#r' to LOAD assemblies — the biggest hole.
        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("ref", "#r \"System.Net.Http\"\n"));

        Assert.Contains("#r", ex.Message);
        Assert.Empty(scripting.Scripts);
    }

    // -- Output & lifecycle ---------------------------------------------------

    [Fact]
    public async Task Log_AppearsInOutput_AndScriptCompletes()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        var script = await scripting.StartAsync("logger", "ns.Log(\"hello from script\");");
        Assert.Contains("hello from script", script.Output);

        // No awaits: execution finishes without needing any tick.
        Assert.True(await RunUntilAsync(session, scripting, () => !script.IsRunning));
        Assert.Null(script.Error);
    }

    [Fact]
    public async Task Hack_SubmitsAtTickBoundary_AndHacksStart()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);
        long before = session.State.EventsLogged;

        // The script's Hack must become a real engine hack — a script is just
        // another command source, so a valid target must actually start.
        var script = await scripting.StartAsync("hacker",
            "await ns.Hack(\"webmail\"); ns.Log(\"hack submitted at \" + ns.Ticks);");

        Assert.True(await RunUntilAsync(session, scripting,
            () => session.State.ActiveJobs.Count > 0),
            "Script-submitted hack never started");
        Assert.True(before < session.State.EventsLogged, "Hack produced no events");
        Assert.Contains(script.Output, l => l.Contains("hack submitted at"));
    }

    [Fact]
    public async Task Wait_SuspendsForAtLeastRequestedTicks()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        // Wait(5) can only be satisfied by >= 5 barrier releases (it may
        // take one more if the script starts mid-tick — tolerated).
        var script = await scripting.StartAsync("waiter",
            "ns.Log(\"t0=\" + ns.Ticks); await ns.Wait(5); ns.Log(\"t1=\" + ns.Ticks);");

        Assert.True(await RunUntilAsync(session, scripting,
            () => script.Output.Any(l => l.Contains("t1="))),
            "Script never finished its Wait(5)");

        long t0 = ParseTicks(script, "t0=");
        long t1 = ParseTicks(script, "t1=");
        Assert.True(t1 - t0 >= 5, $"Wait(5) resolved after only {t1 - t0} ticks");
    }

    [Fact]
    public async Task Stop_UnwindsInfiniteLoop_AndRemovesInstance()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        var script = await scripting.StartAsync("daemon",
            "int i = 0;\nwhile (true) { ns.Log(\"beat \" + i++); await ns.Wait(1); }");

        // Prove it's alive and looping...
        Assert.True(await RunUntilAsync(session, scripting,
            () => script.Output.Count(l => l.Contains("beat ")) >= 3));
        int beats = script.Output.Count(l => l.Contains("beat "));
        var instance = script; // keep a handle: it gets removed from Scripts below

        // ...then stop it. The stop takes effect at the next await point.
        scripting.Stop("daemon");

        Assert.True(await RunUntilAsync(session, scripting,
            () => !instance.IsRunning && scripting.Find("daemon") is null),
            "Stopped script never unwound / was never removed");
        Assert.Contains(instance.Output, l => l.Contains("stopped"));

        // And the engine keeps ticking afterwards.
        long tick = session.State.GameTick;
        await TickAsync(session, scripting);
        Assert.True(session.State.GameTick > tick);
        int after = instance.Output.Count(l => l.Contains("beat "));
        Assert.Equal(beats, after); // no beats after the stop
    }

    [Fact]
    public async Task RuntimeError_HaltsOnlyThatScript()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);

        var bad = await scripting.StartAsync("bad",
            "throw new Exception(\"boom\");");
        var good = await scripting.StartAsync("good",
            "for (int i = 0; i < 3; i++) { ns.Log(\"alive \" + i); await ns.Wait(1); }");

        Assert.True(await RunUntilAsync(session, scripting, () => !bad.IsRunning));
        Assert.NotNull(bad.Error);
        Assert.Contains("boom", bad.Error!.Message);

        // The crash must not take down its sibling or the engine.
        Assert.True(await RunUntilAsync(session, scripting, () => !good.IsRunning),
            "Healthy script died alongside the crashing one");
        Assert.Equal(3, good.Output.Count(l => l.Contains("alive ")));
        Assert.Null(good.Error);
    }

    // -- CPU quota --------------------------------------------------------

    [Fact]
    public async Task Quota_OverBudgetBurst_StillProcessesAllCommands()
    {
        var session = NewSession();
        var scripting = new ScriptingSystem(session);
        long before = session.State.EventsLogged;

        // Fire 60 buys without awaiting (a legit scripting pattern for
        // bursts). Over quota, each Action parks at the barrier BEFORE
        // submitting, so the extra calls are deferred to later ticks —
        // throttled, not dropped, not crashed.
        var script = await scripting.StartAsync("burst",
            "for (int i = 0; i < 60; i++) { _ = ns.Buy(\"cpu\"); }\nns.Log(\"burst queued\");");

        // NOTE: observable assumes a rejected buy (no money at start) logs an
        // EventLog entry. If engine feedback changes, swap the observable.
        Assert.True(await RunUntilAsync(session, scripting,
            () => session.State.EventsLogged - before >= 60),
            $"Quota-deferred commands never all arrived (got {session.State.EventsLogged - before})");
        Assert.Contains(script.Output, l => l.Contains("burst queued"));
    }

    // -- Persistence ----------------------------------------------------------

    [Fact]
    public async Task Persistence_RoundTripsSourceDataAndRunningFlag()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nullbyte-script-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string oldCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(dir); // ScriptPersistence writes relative paths
        try
        {
            var session = NewSession();
            var scripting = new ScriptingSystem(session);

            // Keep it running (trailing Wait) so Running=true in the save.
            await scripting.StartAsync("persist",
                "ns.Data.SetNum(\"runs\", ns.Data.GetNum(\"runs\", 0) + 1);\nawait ns.Wait(100);");
            await TickAsync(session, scripting);
            scripting.Stop("persist");
            await TickAsync(session, scripting);

            ScriptPersistence.Save(scripting.Scripts);
            var loaded = ScriptPersistence.Load();

            var entry = Assert.Single(loaded);
            Assert.Equal("persist", entry.Name);
            Assert.True(entry.Running);
            Assert.Contains("runs", entry.Source!);
            Assert.Equal(1, entry.Data!.GetNum("runs"));
        }
        finally
        {
            Directory.SetCurrentDirectory(oldCwd);
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private static long ParseTicks(ScriptInstance script, string prefix)
    {
        var line = script.Output.First(l => l.Contains(prefix));
        string value = line[(line.IndexOf(prefix) + prefix.Length)..];
        int end = 0;
        while (end < value.Length && char.IsDigit(value[end])) end++;
        return long.Parse(value[..end]);
    }
}