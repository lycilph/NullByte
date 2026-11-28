using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Runtime;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Runtime;

/// <summary>Starting scripts and following their life cycle. Paths and save/restore have their own classes.</summary>
public sealed class ScriptServiceTests
{
    // Starting a script

    [Fact]
    public async Task Start_WithExistingScript_ExecutesIt()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("hello.cx", "nb.Print(\"Hello from script\");");

        var process = await world.StartSucceedingAsync("hello.cx");
        await world.FinishedAsync(process);

        Assert.Equal(["Hello from script"], world.Probe.Output);
    }

    [Fact]
    public async Task Start_PassesTheArgumentsToTheScript()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("args.cx", "nb.Print(args[0]);\nnb.Print(args[1]);");

        var process = await world.StartSucceedingAsync("args.cx", "n00dles", "second");
        await world.FinishedAsync(process);

        Assert.Equal(["n00dles", "second"], world.Probe.Output);
    }

    [Fact]
    public async Task Start_SnapshotsTheArguments()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("args.cx", "await nb.Sleep(TimeSpan.FromSeconds(1));\nnb.Print(args[0]);");

        var args = new List<string> { "n00dles" };
        var result = await world.Service.Start(world.Home.Hostname, "args.cx", args);

        Assert.True(result.Succeeded, result.FailureMessage);

        await world.Probe.WaitUntilSleepRequestedAsync();

        args[0] = "changed";
        world.Probe.CompleteSleep();

        await world.FinishedAsync(world.Process(result.Pid));

        Assert.Equal(["n00dles"], world.Probe.Output);
    }

    [Fact]
    public async Task Start_InTutorialModeWithoutCapability_IsRejectedBeforeCompilation()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.State.Narrative.Mode = NarrativeProgressionMode.Tutorial;
        world.WriteScript("hello.cx", "nb.Print(\"Hello\");");

        var result = await world.StartAsync("hello.cx");

        Assert.False(result.Succeeded);
        Assert.Contains("not available", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(world.Service.Processes);
        Assert.Equal(0, world.Home.UsedRam);
    }

    [Fact]
    public async Task Start_WithMissingScript_Fails()
    {
        var world = ScriptTestSetup.CreateWorld();

        var result = await world.StartAsync("missing.cx");

        Assert.False(result.Succeeded);
        Assert.Contains("File not found", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing.cx", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Diagnostics);
        Assert.False(world.Service.HasRunningProcesses);
    }

    [Fact]
    public async Task Start_WithCompilationError_ReturnsTheDiagnostics()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("broken.cx", "nb.Print(missingValue);");

        var result = await world.StartAsync("broken.cx");

        Assert.False(result.Succeeded);
        Assert.Contains("Compilation failed", result.FailureMessage, StringComparison.OrdinalIgnoreCase);

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "CS0103");

        Assert.Equal(ScriptDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(1, diagnostic.Line);
        Assert.False(world.Service.HasRunningProcesses);
    }

    [Fact]
    public async Task Start_WithWarnings_StillStartsAndReturnsThem()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("warning.cx", "var unused = 42;\nnb.Print(\"Running\");");

        var result = await world.StartAsync("warning.cx");

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.Contains(result.Diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task Start_WithUnknownServer_Fails()
    {
        var world = ScriptTestSetup.CreateWorld();

        var result = await world.Service.Start("missing-server", "script.cx", []);

        Assert.False(result.Succeeded);
        Assert.Contains("Unknown server: missing-server", result.FailureMessage);
    }

    [Fact]
    public async Task Start_OnAServerWithoutRootAccess_Fails()
    {
        var world = ScriptTestSetup.CreateWorld();
        var locked = world.AddServer(TestServers.Create("locked", hasRootAccess: false));

        var result = await world.Service.Start(locked.Hostname, "script.cx", []);

        Assert.False(result.Succeeded);
        Assert.Contains("root access", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Start_WithZeroThreads_Fails()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("script.cx", "nb.Print(\"Hello\");");

        var result = await world.Service.Start(world.Home.Hostname, "script.cx", [], threads: 0);

        Assert.False(result.Succeeded);
        Assert.Contains("Thread count", result.FailureMessage);
    }

    [Fact]
    public async Task Start_WhenTheServerHasTooLittleRam_FailsWithoutReservingAnything()
    {
        var world = ScriptTestSetup.CreateWorld(homeRam: 1);
        world.WriteScript("big.cx", "var x = 1;");   // the base cost alone is more than 1 GB

        var result = await world.StartAsync("big.cx");

        Assert.False(result.Succeeded);
        Assert.Contains("Not enough RAM", result.FailureMessage);
        Assert.Equal(0, world.Home.UsedRam);
        Assert.Empty(world.Service.Processes);
    }

    [Fact]
    public async Task Start_WithInvalidFileName_Throws()
    {
        var world = ScriptTestSetup.CreateWorld();

        await Assert.ThrowsAsync<ArgumentException>(() => world.Service.Start(world.Home.Hostname, "", []));
    }

    [Fact]
    public async Task Start_WithNullArguments_Throws()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("script.cx", "nb.Print(\"Hello\");");

        await Assert.ThrowsAsync<ArgumentNullException>(() => world.Service.Start(world.Home.Hostname, "script.cx", null!));
    }

    [Fact]
    public async Task StartSimple_ReturnsThePidWhenTheScriptStarts_AndZeroWhenItDoesNot()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("ok.cx", "nb.Print(\"Hello\");");

        Assert.True(await world.Service.StartSimple(world.Home.Hostname, "ok.cx", []) > 0);
        Assert.Equal(0, await world.Service.StartSimple(world.Home.Hostname, "missing.cx", []));
    }

    // Life cycle

    [Fact]
    public async Task RunningScript_HoldsItsRamUntilItFinishes()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("sleepy.cx", "await nb.Sleep(TimeSpan.FromSeconds(1));");

        var process = await world.StartSucceedingAsync("sleepy.cx");
        await world.Probe.WaitUntilSleepRequestedAsync();

        Assert.True(world.Service.HasRunningProcesses);
        Assert.True(process.RamCost > 0);
        Assert.Equal(process.RamCost, world.Home.UsedRam, precision: 4);

        world.Probe.CompleteSleep();
        await world.FinishedAsync(process);

        Assert.Equal(0, world.Home.UsedRam);
        Assert.False(world.Service.HasRunningProcesses);
    }

    [Fact]
    public async Task CompletedScript_RaisesProcessCompleted_AndReleasesItsRam()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("complete.cx", "nb.Print(\"Finished\");");

        ScriptProcess? completed = null;
        world.Service.ProcessCompleted += p => completed = p;

        var process = await world.StartSucceedingAsync("complete.cx");
        await world.FinishedAsync(process);

        Assert.Same(process, completed);
        Assert.Equal(ScriptProcessState.Completed, process.State);
        Assert.True(process.RamReleased);
        Assert.Equal(0, world.Home.UsedRam);
    }

    [Fact]
    public async Task FailedScript_RaisesProcessFailed_RecordsTheFailureAndReleasesItsRam()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("failure.cx", "throw new InvalidOperationException(\"Script failed.\");");

        Exception? raised = null;
        world.Service.ProcessFailed += (_, exception) => raised = exception;

        var process = await world.StartSucceedingAsync("failure.cx");
        await world.FinishedAsync(process);

        var exception = Assert.IsType<InvalidOperationException>(raised);

        Assert.Equal("Script failed.", exception.Message);
        Assert.Equal(ScriptProcessState.Failed, process.State);
        Assert.Equal("Script failed.", process.Failure?.Message);
        Assert.True(process.RamReleased);
        Assert.Equal(0, world.Home.UsedRam);
    }

    [Fact]
    public async Task CompletedScript_WithAThrowingObserver_KeepsItsOutcome()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("complete.cx", "nb.Print(\"Completed\");");

        ScriptProcess? observed = null;
        world.Service.ProcessCompleted += _ => throw new InvalidOperationException("Observer failed.");
        world.Service.ProcessCompleted += p => observed = p;

        var process = await world.StartSucceedingAsync("complete.cx");
        await world.FinishedAsync(process);

        Assert.Same(process, observed);
        Assert.Equal(ScriptProcessState.Completed, process.State);
        Assert.Null(process.Failure);
        Assert.True(process.RamReleased);
        Assert.Equal(0, world.Home.UsedRam);
    }

    [Fact]
    public async Task FailedScript_WithAThrowingObserver_StillNotifiesTheOtherObservers()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("failure.cx", "throw new InvalidOperationException(\"Script failed.\");");

        ScriptProcess? observed = null;
        world.Service.ProcessFailed += (_, _) => throw new InvalidOperationException("Observer failed.");
        world.Service.ProcessFailed += (p, _) => observed = p;

        var process = await world.StartSucceedingAsync("failure.cx");
        await world.FinishedAsync(process);

        Assert.Same(process, observed);
        Assert.Equal(ScriptProcessState.Failed, process.State);
        Assert.Equal("Script failed.", process.Failure?.Message);
        Assert.True(process.RamReleased);
        Assert.Equal(0, world.Home.UsedRam);
    }

    [Theory]
    [InlineData("nb.Print(\"First\");")]                                   // finishes normally
    [InlineData("throw new InvalidOperationException(\"Boom\");")]         // fails
    public async Task Start_AfterAFinishedScript_AllowsAnotherScriptToStart(string firstScript)
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("first.cx", firstScript);
        world.WriteScript("second.cx", "nb.Print(\"Second\");");

        await world.FinishedAsync(await world.StartSucceedingAsync("first.cx"));
        await world.FinishedAsync(await world.StartSucceedingAsync("second.cx"));

        Assert.Contains("Second", world.Probe.Output);
    }
}
