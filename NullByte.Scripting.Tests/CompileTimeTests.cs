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

// -- Compile-time lockdown ------------------------------------------------
public class CompileTimeTests
{
    [Fact]
    public async Task Compile_UnknownApiMethod_ThrowsWithDiagnostics()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("bad", "api.UndefinedMethod();"));

        Assert.Contains("CS1061", ex.Message); // '... does not contain a definition for ...'
        Assert.Empty(scripting.Scripts);       // failed compiles never register
    }

    [Fact]
    public async Task Compile_BannedUsing_Throws()
    {
        var session = Helper.NewSession();
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
        var session = Helper.NewSession();
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
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session);

        // Roslyn scripting honors '#r' to LOAD assemblies — the biggest hole.
        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("ref", "#r \"System.Net.Http\"\n"));

        Assert.Contains("#r", ex.Message);
        Assert.Empty(scripting.Scripts);
    }
}