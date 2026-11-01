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

// -- CPU quota --------------------------------------------------------
public class QuotaTests
{
    [Fact]
    public async Task Quota_OverBudgetBurst_StillProcessesAllCommands()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        // Fire 60 buys without awaiting (a legit scripting pattern for
        // bursts). Over quota, each Action parks at the barrier BEFORE
        // submitting, so the extra calls are deferred to later ticks —
        // throttled, not dropped, not crashed.
        var script = await scripting.StartAsync("burst",
            "for (int i = 0; i < 60; i++) { _ = api.Buy(\"cpu\"); }\napi.Log(\"burst queued\");");

        // NOTE: observable assumes a rejected buy (no money at start) logs an
        // EventLog entry. If engine feedback changes, swap the observable.
        Assert.True(await Helper.RunUntilAsync(session, scripting,
            () => session.State.EventLog.Count >= 60),
            $"Quota-deferred commands never all arrived (got {session.State.EventLog.Count})");
        Assert.Contains(script.Output, l => l.Contains("burst queued"));
    }
}