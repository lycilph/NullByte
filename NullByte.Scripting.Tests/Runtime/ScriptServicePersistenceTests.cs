using NullByte.Core.Persistence;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Runtime;

/// <summary>Capturing running processes for a save and starting them again after a load, plus RAM measuring and clean-up.</summary>
public sealed class ScriptServicePersistenceTests
{
    private const string SleepyScript = "await nb.Sleep(TimeSpan.FromSeconds(1));";

    [Fact]
    public async Task CaptureRunningProcesses_ReturnsOnlyRunningProcesses_InStartOrder()
    {
        var world = ScriptTestSetup.CreateWorld(homeRam: 64);
        world.WriteScript("first.cx", SleepyScript);
        world.WriteScript("second.cx", SleepyScript);
        world.WriteScript("done.cx", "nb.Print(\"done\");");

        var first = await world.StartSucceedingAsync("first.cx", "alpha", "beta");
        var secondResult = await world.Service.Start(world.Home.Hostname, "second.cx", [], threads: 2);
        Assert.True(secondResult.Succeeded, secondResult.FailureMessage);
        var finished = await world.StartSucceedingAsync("done.cx");   // finishes at once, so it is not captured

        var saved = world.Service.CaptureRunningProcesses();

        Assert.Equal(2, saved.Count);

        Assert.Equal("first.cx", saved[0].Filename);
        Assert.Equal(["alpha", "beta"], saved[0].Args);
        Assert.Equal(1, saved[0].Threads);
        Assert.Equal(world.Home.Id.ToString(), saved[0].ServerId);

        Assert.Equal("second.cx", saved[1].Filename);
        Assert.Equal(2, saved[1].Threads);

        world.Probe.CompleteSleep();
        await world.FinishedAsync(first);
        await world.FinishedAsync(world.Process(secondResult.Pid));
        await world.FinishedAsync(finished);
    }

    [Fact]
    public async Task RestoreProcessesAsync_StartsEachSavedProcess_AndReportsTheOnesThatCannotStart()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("restored.cx", "nb.Print(args[0]);");

        var serverId = world.Home.Id.ToString();
        var broken = new ProcessSave { Filename = "missing.cx", ServerId = serverId, Args = [], Threads = 1 };
        var working = new ProcessSave { Filename = "restored.cx", ServerId = serverId, Args = ["after"], Threads = 1 };

        var results = await world.Service.RestoreProcessesAsync([broken, working]);

        Assert.Equal(2, results.Count);

        Assert.Same(broken, results[0].Saved);
        Assert.False(results[0].Start.Succeeded);

        Assert.Same(working, results[1].Saved);
        Assert.True(results[1].Start.Succeeded, results[1].Start.FailureMessage);

        await world.FinishedAsync(world.Process(results[1].Start.Pid));

        Assert.Equal(["after"], world.Probe.Output);
    }

    [Fact]
    public async Task RestoreProcessesAsync_WithNull_Throws()
    {
        var world = ScriptTestSetup.CreateWorld();

        await Assert.ThrowsAsync<ArgumentNullException>(() => world.Service.RestoreProcessesAsync(null!));
    }

    [Fact]
    public void MeasureRam_WithACompilableScript_ReturnsItsCost()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("ram.cx", "var value = 42;");

        var result = world.Service.MeasureRam("ram.cx", "home");

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.Equal(1.6, result.Ram, precision: 4);
    }

    [Fact]
    public void MeasureRam_WithAMissingFile_Fails()
    {
        var world = ScriptTestSetup.CreateWorld();

        var result = world.Service.MeasureRam("nope.cx", "home");

        Assert.False(result.Succeeded);
        Assert.Equal("File not found: nope.cx", result.FailureMessage);
    }

    [Fact]
    public void MeasureRam_WithAnInvalidPath_Fails()
    {
        var world = ScriptTestSetup.CreateWorld();

        var result = world.Service.MeasureRam("a//b", "home");

        Assert.False(result.Succeeded);
        Assert.StartsWith("Invalid file name: ", result.FailureMessage);
    }

    [Fact]
    public void MeasureRam_WithAScriptThatDoesNotCompile_ReturnsTheDiagnostics()
    {
        var world = ScriptTestSetup.CreateWorld();
        world.WriteScript("broken.cx", "nb.Print(missingValue);");

        var result = world.Service.MeasureRam("broken.cx", "home");

        Assert.False(result.Succeeded);
        Assert.Equal("The script could not be compiled.", result.FailureMessage);
        Assert.Contains(result.Diagnostics, d => d.Id == "CS0103");
    }

    [Fact]
    public async Task RemoveCompleted_RemovesFinishedProcesses_AndKeepsRunningOnes()
    {
        var world = ScriptTestSetup.CreateWorld(homeRam: 64);
        world.WriteScript("sleepy.cx", SleepyScript);
        world.WriteScript("done.cx", "nb.Print(\"done\");");

        var running = await world.StartSucceedingAsync("sleepy.cx");
        var finished = await world.StartSucceedingAsync("done.cx");
        await world.FinishedAsync(finished);

        world.Service.RemoveCompleted();

        Assert.Same(running, Assert.Single(world.Service.Processes));

        world.Probe.CompleteSleep();
        await world.FinishedAsync(running);
    }
}
