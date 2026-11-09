using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests.Runtime;

[Collection(ScriptRuntimeCollection.Name)]
public sealed class ScriptRunnerTests
{
    private readonly ScriptCompiler compiler = new();
    private readonly ScriptRunner runner;

    public ScriptRunnerTests()
    {
        runner = new ScriptRunner(new ScriptAssemblyLoader());
    }

    [Fact]
    public async Task RunAsync_WithSynchronousScript_ExecutesScript()
    {
        var script = Compile(
            """
            nb.Print("Hello from script");
            """);

        var api = new TestNullByteApi();

        await runner.RunAsync(script, api, []);

        Assert.Equal(["Hello from script"], api.Output);
    }

    [Fact]
    public async Task RunAsync_PassesArgumentsToGeneratedArgs()
    {
        var script = Compile(
            """
            nb.Print(args[0]);
            nb.Print(args[1]);
            """);

        var api = new TestNullByteApi();

        await runner.RunAsync(script, api, ["n00dles", "second"]);

        Assert.Equal(["n00dles", "second"], api.Output);
    }

    [Fact]
    public async Task RunAsync_SnapshotsArguments()
    {
        var script = Compile(
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            nb.Print(args[0]);
            """);

        var api = new TestNullByteApi();
        var args = new List<string> {"n00dles"};

        var runTask = runner.RunAsync(script, api,args);

        await api.WaitUntilSleepRequestedAsync();

        args[0] = "changed";

        api.CompleteSleep();

        await runTask;

        Assert.Equal(["n00dles"], api.Output);
    }

    [Fact]
    public async Task RunAsync_WithTopLevelAwait_DoesNotBlock()
    {
        var script = Compile(
            """
            nb.Print("Before");
            await nb.Sleep(TimeSpan.FromSeconds(1));
            nb.Print("After");
            """);

        var api = new TestNullByteApi();
        var runTask = runner.RunAsync(script, api, []);

        await api.WaitUntilSleepRequestedAsync();

        Assert.False(runTask.IsCompleted);
        Assert.Equal(["Before"], api.Output);

        api.CompleteSleep();

        await runTask;

        Assert.Equal(["Before", "After"], api.Output);
    }

    [Fact]
    public async Task RunAsync_PassesDurationToSleep()
    {
        var script = Compile(
            """
            await nb.Sleep(TimeSpan.FromSeconds(12));
            """);

        var api = new TestNullByteApi();
        var runTask = runner.RunAsync(script, api, []);

        await api.WaitUntilSleepRequestedAsync();

        Assert.Equal(TimeSpan.FromSeconds(12), api.LastSleepDuration);

        api.CompleteSleep();

        await runTask;
    }

    [Fact]
    public async Task RunAsync_KeepsApiBoundWhileScriptIsSuspended()
    {
        var script = Compile(
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            """);

        var api = new TestNullByteApi();

        var runTask = runner.RunAsync(script, api,[]);

        await api.WaitUntilSleepRequestedAsync();

        Assert.Same(api, ScriptRuntime.CurrentApi);

        api.CompleteSleep();

        await runTask;
    }

    [Fact]
    public async Task RunAsync_UnbindsApiAfterSynchronousScript()
    {
        var script = Compile(
            """
            nb.Print("Finished");
            """);

        var api = new TestNullByteApi();

        await runner.RunAsync(script, api, []);

        AssertRuntimeIsUnbound();
    }

    [Fact]
    public async Task RunAsync_UnbindsApiAfterAsynchronousScript()
    {
        var script = Compile(
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            """);

        var api = new TestNullByteApi();
        var runTask = runner.RunAsync(script, api, []);

        await api.WaitUntilSleepRequestedAsync();

        api.CompleteSleep();

        await runTask;

        AssertRuntimeIsUnbound();
    }

    [Fact]
    public async Task RunAsync_WithSynchronousException_PropagatesException()
    {
        var script = Compile(
            """
            throw new InvalidOperationException("Script failed.");
            """);

        var api = new TestNullByteApi();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.RunAsync(script, api, []));

        Assert.Equal("Script failed.", exception.Message);

        AssertRuntimeIsUnbound();
    }

    [Fact]
    public async Task RunAsync_WithAsynchronousException_PropagatesException()
    {
        var script = Compile(
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            throw new InvalidOperationException("Async script failed.");
            """);

        var api = new TestNullByteApi();

        var runTask = runner.RunAsync(script, api, []);

        await api.WaitUntilSleepRequestedAsync();

        api.CompleteSleep();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => runTask);

        Assert.Equal("Async script failed.", exception.Message);

        AssertRuntimeIsUnbound();
    }

    [Fact]
    public async Task RunAsync_WhenSleepIsCanceled_PropagatesCancellation()
    {
        var script = Compile(
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            """);

        var api = new TestNullByteApi();

        var runTask = runner.RunAsync(script, api, []);

        await api.WaitUntilSleepRequestedAsync();

        api.CancelSleep();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        AssertRuntimeIsUnbound();
    }

    [Fact]
    public async Task RunAsync_AfterCompletedScript_CanRunAnotherScript()
    {
        var firstScript = Compile(
            """
            nb.Print("First");
            """);

        var secondScript = Compile(
            """
            nb.Print("Second");
            """);

        var firstApi = new TestNullByteApi();
        var secondApi = new TestNullByteApi();

        await runner.RunAsync(firstScript, firstApi, []);
        await runner.RunAsync(secondScript, secondApi, []);

        Assert.Equal(["First"], firstApi.Output);
        Assert.Equal(["Second"], secondApi.Output);

        AssertRuntimeIsUnbound();
    }

    [Fact]
    public async Task RunAsync_WhileAnotherScriptIsRunning_Throws()
    {
        var firstScript = Compile(
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            """);

        var secondScript = Compile(
            """
            nb.Print("Second");
            """);

        var firstApi = new TestNullByteApi();
        var secondApi = new TestNullByteApi();

        var firstRunTask = runner.RunAsync(firstScript, firstApi, []);

        await firstApi.WaitUntilSleepRequestedAsync();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.RunAsync(secondScript, secondApi, []));

        Assert.Contains("already bound", exception.Message, StringComparison.OrdinalIgnoreCase);

        firstApi.CompleteSleep();

        await firstRunTask;

        AssertRuntimeIsUnbound();
    }

    [Fact]
    public async Task RunAsync_WithNullScript_Throws()
    {
        var api = new TestNullByteApi();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => runner.RunAsync(null!, api,[]));
    }

    [Fact]
    public async Task RunAsync_WithNullApi_Throws()
    {
        var script = Compile(
            """
            Console.WriteLine("Hello");
            """);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => runner.RunAsync(script, null!, []));
    }

    [Fact]
    public async Task RunAsync_WithNullArgs_Throws()
    {
        var script = Compile(
            """
            Console.WriteLine("Hello");
            """);

        var api = new TestNullByteApi();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => runner.RunAsync(script, api, null!));
    }

    private CompiledScript Compile(string source)
    {
        var result = compiler.Compile(source, "runner-test.cs");

        AssertCompilationSucceeded(result);

        return Assert.IsType<CompiledScript>(result.Script);
    }

    private static void AssertCompilationSucceeded(
        ScriptCompilationResult result)
    {
        var diagnostics = string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(
                diagnostic =>
                    $"{diagnostic.Id} " +
                    $"({diagnostic.Line}," +
                    $"{diagnostic.Column}): " +
                    diagnostic.Message));

        Assert.True(result.Succeeded, diagnostics);
    }

    private static void AssertRuntimeIsUnbound()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ScriptRuntime.CurrentApi);

        Assert.Contains(
            "No NullByte API is bound",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestNullByteApi : INullByteApi
    {
        private TaskCompletionSource sleepRequested = CreateCompletionSource();
        private TaskCompletionSource sleepCompletion = CreateCompletionSource();

        public List<string> Output { get; } = [];

        public TimeSpan? LastSleepDuration { get; private set; }

        public void Print(object? value)
        {
            Output.Add(value?.ToString() ?? string.Empty);
        }

        public Task Sleep(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            LastSleepDuration = duration;

            sleepRequested.TrySetResult();

            return sleepCompletion.Task.WaitAsync(cancellationToken);
        }

        public Task<HackResult> Hack(string hostname, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Hack is not used by these runner tests.");
        }

        public Task<GrowResult> Grow(string hostname, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Grow is not used by these runner tests.");
        }

        public Task<WeakenResult> Weaken(string hostname, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Weaken is not used by these runner tests.");
        }

        public double GetServerMoneyAvailable(string hostname)
        {
            throw new NotSupportedException("Server state is not used by these runner tests.");
        }

        public double GetServerMaxMoney(string hostname)
        {
            throw new NotSupportedException("Server state is not used by these runner tests.");
        }

        public double GetServerSecurityLevel(string hostname)
        {
            throw new NotSupportedException("Server state is not used by these runner tests.");
        }

        public double GetServerMinSecurityLevel(string hostname)
        {
            throw new NotSupportedException("Server state is not used by these runner tests.");
        }

        public Task WaitUntilSleepRequestedAsync()
        {
            return sleepRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void CompleteSleep()
        {
            sleepCompletion.TrySetResult();
        }

        public void CancelSleep()
        {
            sleepCompletion.TrySetCanceled();
        }

        private static TaskCompletionSource CreateCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}