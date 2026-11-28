using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Runtime;

public sealed class ScriptRunnerTests
{
    private readonly ScriptRunner runner = new(new ScriptAssemblyLoader());

    [Fact]
    public async Task RunAsync_WithSynchronousScript_ExecutesScript()
    {
        var script = TestScripts.CompileOrFail("nb.Print(\"Hello from script\");");
        var probe = new ScriptApiProbe();

        await runner.RunAsync(script, probe.Api, []);

        Assert.Equal(["Hello from script"], probe.Output);
    }

    [Fact]
    public async Task RunAsync_PassesArgumentsToTheScript()
    {
        var script = TestScripts.CompileOrFail("nb.Print(args[0]);\nnb.Print(args[1]);");
        var probe = new ScriptApiProbe();

        await runner.RunAsync(script, probe.Api, ["n00dles", "second"]);

        Assert.Equal(["n00dles", "second"], probe.Output);
    }

    [Fact]
    public async Task RunAsync_SnapshotsTheArguments()
    {
        var script = TestScripts.CompileOrFail("await nb.Sleep(TimeSpan.FromSeconds(1));\nnb.Print(args[0]);");
        var probe = new ScriptApiProbe();
        var args = new List<string> { "n00dles" };

        var runTask = runner.RunAsync(script, probe.Api, args);

        await probe.WaitUntilSleepRequestedAsync();

        args[0] = "changed";
        probe.CompleteSleep();

        await runTask;

        Assert.Equal(["n00dles"], probe.Output);
    }

    [Fact]
    public async Task RunAsync_WithTopLevelAwait_DoesNotBlock()
    {
        var script = TestScripts.CompileOrFail(
            "nb.Print(\"Before\");\nawait nb.Sleep(TimeSpan.FromSeconds(1));\nnb.Print(\"After\");");
        var probe = new ScriptApiProbe();

        var runTask = runner.RunAsync(script, probe.Api, []);

        await probe.WaitUntilSleepRequestedAsync();

        Assert.False(runTask.IsCompleted);
        Assert.Equal(["Before"], probe.Output);

        probe.CompleteSleep();

        await runTask;

        Assert.Equal(["Before", "After"], probe.Output);
    }

    [Fact]
    public async Task RunAsync_PassesTheDurationToSleep()
    {
        var script = TestScripts.CompileOrFail("await nb.Sleep(TimeSpan.FromSeconds(12));");
        var probe = new ScriptApiProbe();

        var runTask = runner.RunAsync(script, probe.Api, []);

        await probe.WaitUntilSleepRequestedAsync();

        Assert.Equal(TimeSpan.FromSeconds(12), probe.LastSleepDuration);

        probe.CompleteSleep();

        await runTask;
    }

    [Fact]
    public async Task RunAsync_WithSynchronousException_PropagatesIt()
    {
        var script = TestScripts.CompileOrFail("throw new InvalidOperationException(\"Script failed.\");");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(script, new ScriptApiProbe().Api, []));

        Assert.Equal("Script failed.", exception.Message);
    }

    [Fact]
    public async Task RunAsync_WithAsynchronousException_PropagatesIt()
    {
        var script = TestScripts.CompileOrFail(
            "await nb.Sleep(TimeSpan.FromSeconds(1));\nthrow new InvalidOperationException(\"Async script failed.\");");
        var probe = new ScriptApiProbe();

        var runTask = runner.RunAsync(script, probe.Api, []);

        await probe.WaitUntilSleepRequestedAsync();
        probe.CompleteSleep();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => runTask);

        Assert.Equal("Async script failed.", exception.Message);
    }

    [Fact]
    public async Task RunAsync_WhenSleepIsCanceled_PropagatesTheCancellation()
    {
        var script = TestScripts.CompileOrFail("await nb.Sleep(TimeSpan.FromSeconds(1));");
        var probe = new ScriptApiProbe();

        var runTask = runner.RunAsync(script, probe.Api, []);

        await probe.WaitUntilSleepRequestedAsync();
        probe.CancelSleep();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task RunAsync_AfterACompletedScript_CanRunAnotherScript()
    {
        var first = new ScriptApiProbe();
        var second = new ScriptApiProbe();

        await runner.RunAsync(TestScripts.CompileOrFail("nb.Print(\"First\");"), first.Api, []);
        await runner.RunAsync(TestScripts.CompileOrFail("nb.Print(\"Second\");"), second.Api, []);

        Assert.Equal(["First"], first.Output);
        Assert.Equal(["Second"], second.Output);
    }

    [Fact]
    public async Task RunAsync_WithNullArgument_Throws()
    {
        var script = TestScripts.CompileOrFail("nb.Print(\"Hello\");");
        var api = new ScriptApiProbe().Api;

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(null!, api, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(script, null!, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(script, api, null!));
    }
}
