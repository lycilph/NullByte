namespace NullByte.Scripting.Tests;

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

// -- Output & lifecycle ---------------------------------------------------
public class OutputAndLifecycleTests
{
    [Fact]
    public async Task Log_AppearsInOutput_AndScriptCompletes()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        var script = await scripting.StartAsync("logger", "api.Log(\"hello from script\");");
        await Helper.TickAsync(session, scripting);
        Assert.Contains("[logger] hello from script", script.Output);

        // No awaits: execution finishes without needing any tick.
        Assert.True(await Helper.RunUntilAsync(session, scripting, () => !script.IsRunning));
        Assert.Null(script.Error);
    }

    [Fact]
    public async Task Hack_SubmitsAtTickBoundary_AndHacksStart()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        // The script's Hack must become a real engine hack — a script is just
        // another command source, so a valid target must actually start.
        var script = await scripting.StartAsync("hacker",
            "await api.Hack(\"webmail\"); api.Log(\"hack submitted at \" + api.Ticks);");

        Assert.True(await Helper.RunUntilAsync(session, scripting,
            () => session.State.ActiveJobs.Count > 0),
            "Script-submitted hack never started");
        Assert.False(session.State.EventLog.IsEmpty, "Hack produced no events");
        Assert.Contains(script.Output, l => l.Contains("[hacker] hack submitted at"));
    }

    [Fact]
    public async Task Wait_SuspendsForAtLeastRequestedTicks()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        // Wait(5) can only be satisfied by >= 5 barrier releases (it may
        // take one more if the script starts mid-tick — tolerated).
        var script = await scripting.StartAsync("waiter",
            "api.Log(\"t0=\" + api.Ticks); await api.Wait(5); api.Log(\"t1=\" + api.Ticks);");

        Assert.True(await Helper.RunUntilAsync(session, scripting,
            () => script.Output.Any(l => l.Contains("t1="))),
            "Script never finished its Wait(5)");

        long t0 = Helper.ParseTicks(script, "t0=");
        long t1 = Helper.ParseTicks(script, "t1=");
        Assert.True(t1 - t0 >= 5, $"Wait(5) resolved after only {t1 - t0} ticks");
    }

    [Fact]
    public async Task Stop_UnwindsInfiniteLoop_AndRemovesInstance()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        var script = await scripting.StartAsync("daemon",
            "int i = 0;\nwhile (true) { api.Log(\"beat \" + i++); await api.Wait(1); }");

        // Prove it's alive and looping...
        Assert.True(await Helper.RunUntilAsync(session, scripting,
            () => script.Output.Count(l => l.Contains("beat ")) >= 3));
        int beats = script.Output.Count(l => l.Contains("beat "));
        var instance = script; // keep a handle: it gets removed from Scripts below

        // ...then stop it. The stop takes effect at the next await point.
        scripting.Stop("daemon");

        Assert.True(await Helper.RunUntilAsync(session, scripting,
            () => !instance.IsRunning && scripting.Find("daemon") is null),
            "Stopped script never unwound / was never removed");
        Assert.Contains(instance.Output, l => l.Contains("stopped"));

        // And the engine keeps ticking afterwards.
        long tick = session.State.GameTick;
        await Helper.TickAsync(session, scripting);
        Assert.True(session.State.GameTick > tick);
        int after = instance.Output.Count(l => l.Contains("beat "));
        Assert.Equal(beats, after); // no beats after the stop
    }

    [Fact]
    public async Task RuntimeError_HaltsOnlyThatScript()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        var bad = await scripting.StartAsync("bad",
            "throw new Exception(\"boom\");");
        var good = await scripting.StartAsync("good",
            "for (int i = 0; i < 3; i++) { api.Log(\"alive \" + i); await api.Wait(1); }");

        Assert.True(await Helper.RunUntilAsync(session, scripting, () => !bad.IsRunning));
        Assert.NotNull(bad.Error);
        Assert.Contains("boom", bad.Error!.Message);

        // The crash must not take down its sibling or the engine.
        Assert.True(await Helper.RunUntilAsync(session, scripting, () => !good.IsRunning),
            "Healthy script died alongside the crashing one");
        Assert.Equal(3, good.Output.Count(l => l.Contains("alive ")));
        Assert.Null(good.Error);
    }
}