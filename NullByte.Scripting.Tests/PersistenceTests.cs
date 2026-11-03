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

// -- Persistence ----------------------------------------------------------
public class PersistenceTests : IDisposable
{
    private readonly string test_path = Path.Combine(Path.GetTempPath(), "nullbyte-script-tests", Guid.NewGuid().ToString("N"));
    private readonly string base_path;

    public PersistenceTests()
    {
        base_path = Path.Combine(test_path, "scripts");
        Directory.CreateDirectory(base_path);
    }

    public void Dispose()
    {
        try { Directory.Delete(test_path, true); } catch (IOException) { }
    }

    [Fact]
    public async Task Persistence_RoundTripsSourceDataAndRunningFlag()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session, base_path);

        // Keep it running (trailing Wait) so Running=true in the save.
        await scripting.StartAsync("persist",
            "nb.Data.SetNum(\"runs\", nb.Data.GetNum(\"runs\", 0) + 1);\nawait nb.Wait(100);");
        await Helper.TickAsync(session, scripting);

        ScriptPersistence.Save(scripting.Scripts, base_path);
        var loaded = ScriptPersistence.Load(base_path);

        var entry = Assert.Single(loaded);
        Assert.Equal("persist", entry.Name);
        Assert.True(entry.Running);
        Assert.Contains("runs", entry.Source!);
        Assert.Equal(1, entry.Data!.GetNum("runs"));
    }
}