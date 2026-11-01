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
public class PersistenceTests
{
    [Fact]
    public async Task Persistence_RoundTripsSourceDataAndRunningFlag()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nullbyte-script-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string oldCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(dir); // ScriptPersistence writes relative paths
        try
        {
            var session = Helper.NewSession();
            var scripting = new ScriptingSystem(session);

            // Keep it running (trailing Wait) so Running=true in the save.
            await scripting.StartAsync("persist",
                "api.Data.SetNum(\"runs\", api.Data.GetNum(\"runs\", 0) + 1);\nawait api.Wait(100);");
            await Helper.TickAsync(session, scripting);

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
}