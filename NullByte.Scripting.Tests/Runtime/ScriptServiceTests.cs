using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Core.Scripting.Activities;
using NullByte.Core.Services;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests.Runtime;

public sealed class ScriptServiceTests
{
    [Fact]
    public void UsesIncludes_OnlyForWellFormedDirectives()
    {
        Assert.True(SourceSnapshot.UsesIncludes("#include \"lib.cx\"\nvar x = 1;"));
        Assert.False(SourceSnapshot.UsesIncludes("var x = 1;"));
        Assert.False(SourceSnapshot.UsesIncludes("// #include \"lib.cx\""));
    }

    [Fact]
    public async Task Start_InTutorialModeWithoutCapability_IsRejectedBeforeCompilation()
    {
        var fixture = new Fixture();
        fixture.GameState.Narrative.Mode = NarrativeProgressionMode.Tutorial;
        fixture.WriteScript("hello.cx", """nb.Print("Hello");""");

        var result = await fixture.Service.Start(fixture.Home.Hostname, "hello.cx", []);

        Assert.False(result.Succeeded);
        Assert.Contains("not available", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Service.Processes);
        Assert.Equal(0, fixture.Home.UsedRam);
    }

    [Fact]
    public async Task Start_WithExistingScript_ExecutesScript()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "hello.cx",
            """
            nb.Print("Hello from script");
            """);

        var result = await fixture.Service.Start(fixture.Home.Hostname, "hello.cx", []);

        Assert.True(result.Succeeded, result.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["Hello from script"], fixture.Api.Output);
    }

    [Fact]
    public async Task Start_WithMissingScript_FailsAsync()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.Start(
            fixture.Home.Hostname,
            "missing.cx",
            []);

        Assert.False(result.Succeeded);
        Assert.Contains("File not found", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing.cx", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Diagnostics);
        Assert.False(fixture.Service.HasRunningProcesses);
    }

    [Fact]
    public async Task Start_WithCompilationError_ReturnsDiagnosticsAsync()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "broken.cx",
            """
            nb.Print(missingValue);
            """);

        var result = await fixture.Service.Start(fixture.Home.Hostname, "broken.cx", []);

        Assert.False(result.Succeeded);
        Assert.Contains("Compilation failed", result.FailureMessage, StringComparison.OrdinalIgnoreCase);

        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "CS0103");
        Assert.Equal(ScriptDiagnosticSeverity.Error, diagnostic.Severity);

        Assert.Equal(1, diagnostic.Line);
        Assert.False(fixture.Service.HasRunningProcesses);
    }

    [Fact]
    public async Task Start_PassesArgumentsToScript()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "args.cx",
            """
            nb.Print(args[0]);
            nb.Print(args[1]);
            """);

        var result = await fixture.Service.Start(fixture.Home.Hostname, "args.cx", ["n00dles", "second"]);

        Assert.True(result.Succeeded, result.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["n00dles", "second"], fixture.Api.Output);
    }

    [Fact]
    public async Task Start_SnapshotsArguments()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "args.cx",
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            nb.Print(args[0]);
            """);

        var args = new List<string> { "n00dles" };
        var result = await fixture.Service.Start(fixture.Home.Hostname, "args.cx", args);

        Assert.True(result.Succeeded, result.FailureMessage);

        await fixture.Api.WaitUntilSleepRequestedAsync();

        args[0] = "changed";

        fixture.Api.CompleteSleep();

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["n00dles"], fixture.Api.Output);
    }

    [Fact]
    public async Task CompletedScript_AllowsAnotherScriptToStart()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "first.cx",
            """
            nb.Print("First");
            """);

        fixture.WriteScript(
            "second.cx",
            """
            nb.Print("Second");
            """);

        var firstResult = await fixture.Service.Start(fixture.Home.Hostname, "first.cx", []);

        Assert.True(firstResult.Succeeded, firstResult.FailureMessage);

        await fixture.WaitForCompletionAsync();

        var secondResult = await fixture.Service.Start(fixture.Home.Hostname, "second.cx", []);

        Assert.True(secondResult.Succeeded, secondResult.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["First", "Second"], fixture.Api.Output);
    }

    [Fact]
    public async Task CompletedScript_RaisesScriptCompleted()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "complete.cx",
            """
            nb.Print("Finished");
            """);

        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Service.ProcessCompleted += (_) => completed.TrySetResult();

        var result = await fixture.Service.Start(fixture.Home.Hostname, "complete.cx", []);

        Assert.True(result.Succeeded, result.FailureMessage);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(fixture.Service.HasRunningProcesses);
    }

    [Fact]
    public async Task FailedScript_RaisesScriptFailed()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "failure.cx",
            """
            throw new InvalidOperationException("Script failed.");
            """);

        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Service.ProcessFailed += (proc, exception) => failed.TrySetResult(exception);

        var result = await fixture.Service.Start(fixture.Home.Hostname, "failure.cx", []);

        Assert.True(result.Succeeded, result.FailureMessage);

        var exception = await failed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("Script failed.", exception.Message);

        Assert.False(fixture.Service.HasRunningProcesses);
    }

    [Fact]
    public async Task FailedScript_RecordsFailureAndReleasesRam()
    {
        var fixture = new Fixture();
        fixture.WriteScript(
            "failure.cx",
            """
            throw new InvalidOperationException("Contained failure.");
            """);

        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptProcess? failedProcess = null;
        fixture.Service.ProcessFailed += (process, _) =>
        {
            failedProcess = process;
            failed.TrySetResult();
        };

        var result = await fixture.Service.Start(fixture.Home.Hostname, "failure.cx", []);

        Assert.True(result.Succeeded, result.FailureMessage);
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await failedProcess!.Execution!;

        Assert.Equal(ScriptProcessState.Failed, failedProcess.State);
        Assert.Equal("Contained failure.", failedProcess.Failure?.Message);
        Assert.True(failedProcess.RamReleased);
        Assert.Equal(0, fixture.Home.UsedRam);
    }

    [Fact]
    public async Task CompletedScript_ThrowingObserverDoesNotChangeProcessOutcome()
    {
        var fixture = new Fixture();
        fixture.WriteScript("complete.cx", """nb.Print("Completed");""");

        var completed = new TaskCompletionSource<ScriptProcess>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.ProcessCompleted += _ => throw new InvalidOperationException("Observer failed.");
        fixture.Service.ProcessCompleted += process => completed.TrySetResult(process);

        var result = await fixture.Service.Start(fixture.Home.Hostname, "complete.cx", []);

        Assert.True(result.Succeeded, result.FailureMessage);
        var process = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await process.Execution!;

        Assert.Equal(ScriptProcessState.Completed, process.State);
        Assert.Null(process.Failure);
        Assert.True(process.RamReleased);
        Assert.Equal(0, fixture.Home.UsedRam);
    }

    [Fact]
    public async Task FailedScript_ThrowingObserverDoesNotPreventOtherObservers()
    {
        var fixture = new Fixture();
        fixture.WriteScript(
            "failure.cx",
            """throw new InvalidOperationException("Script failed.");""");

        var failed = new TaskCompletionSource<ScriptProcess>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.ProcessFailed += (_, _) => throw new InvalidOperationException("Observer failed.");
        fixture.Service.ProcessFailed += (process, _) => failed.TrySetResult(process);

        var result = await fixture.Service.Start(fixture.Home.Hostname, "failure.cx", []);

        Assert.True(result.Succeeded, result.FailureMessage);
        var process = await failed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await process.Execution!;

        Assert.Equal(ScriptProcessState.Failed, process.State);
        Assert.Equal("Script failed.", process.Failure?.Message);
        Assert.True(process.RamReleased);
        Assert.Equal(0, fixture.Home.UsedRam);
    }

    [Fact]
    public async Task FailedScript_AllowsAnotherScriptToStart()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "failure.cx",
            """
            throw new InvalidOperationException("Boom");
            """);

        fixture.WriteScript(
            "recovery.cx",
            """
            nb.Print("Recovered");
            """);

        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Service.ProcessFailed += (_, _) => failed.TrySetResult();

        var firstResult = await fixture.Service.Start(fixture.Home.Hostname, "failure.cx", []);

        Assert.True(firstResult.Succeeded, firstResult.FailureMessage);

        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var secondResult = await fixture.Service.Start(fixture.Home.Hostname, "recovery.cx", []);

        Assert.True(secondResult.Succeeded, secondResult.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Contains("Recovered", fixture.Api.Output);
    }

    [Fact]
    public async Task Start_PreservesCompilerWarningsAsync()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "warning.cx",
            """
            var unused = 42;
            nb.Print("Running");
            """);

        var result = await fixture.Service.Start(fixture.Home.Hostname, "warning.cx", []);

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task Start_WithInvalidServer_ThrowsAsync()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.Start("missing-server", "script.cx", []);

        Assert.False(result.Succeeded);
        Assert.Contains("Unknown server: missing-server", result.FailureMessage);
    }

    [Fact]
    public async Task Start_WithInvalidFileName_Throws()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Service.Start(fixture.Home.Hostname, "", []));
    }

    [Fact]
    public async Task Start_WithNullArguments_Throws()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "script.cx",
            """
            nb.Print("Hello");
            """);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.Service.Start(fixture.Home.Hostname, "script.cx", null!));
    }

    private sealed class Fixture
    {
        private readonly TaskCompletionSource scriptFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Fixture()
        {
            Home = new Server(
                hostname: "home",
                maximumMoney: 0,
                startingMoney: 0,
                baseDifficulty: 1,
                minimumSecurity: 1,
                startingSecurity: 1,
                requiredHackingLevel: 1,
                growthParameter: 0,
                maximumRam: 8,
                hasRootAccess: true);

            var network = new Network();
            network.AddServer(Home);

            GameState = new GameState(
                new Player(Home.Id),
                network);
            GameState.Narrative.Mode = NarrativeProgressionMode.Freeplay;
            GameState.Narrative.Mode = NarrativeProgressionMode.Freeplay;

            Api = new TestNullByteApi();
            var apiFactory = new TestNullByteApiFactory(Api);

            var compiler = new ScriptCompiler();
            var loader = new ScriptAssemblyLoader();
            var runner = new ScriptRunner(loader);
            var manager = new ScriptProcessManager();

            Service = new ScriptService(
                GameState,
                compiler,
                runner,
                apiFactory,
                manager);

            Service.ProcessCompleted += _ => scriptFinished.TrySetResult();

            Service.ProcessFailed += (_, _) => scriptFinished.TrySetResult();
        }

        public Server Home { get; }

        public GameState GameState { get; }

        public TestNullByteApi Api { get; }

        public ScriptService Service { get; }

        public void WriteScript(string fileName, string source)
        {
            GameState.FileSystem.WriteFile(Home.Hostname, fileName, source);
        }

        public Task WaitForCompletionAsync()
        {
            return scriptFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class TestNullByteApiFactory(TestNullByteApi api) : INullByteApiFactory
    {
        public INullByteApi Create(IScriptOperations scriptOperations, ServerId hostServerId, int threads, CancellationToken cancellationToken) => api;
    }

    private sealed class TestNullByteApi : INullByteApi
    {
        private readonly TaskCompletionSource sleepRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource sleepCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken cancellationToken = new();

        public List<string> Output { get; } = [];

        public void Print(object? value) => Output.Add(value?.ToString() ?? string.Empty);

        public Task Sleep(TimeSpan duration)
        {
            sleepRequested.TrySetResult();
            return sleepCompletion.Task.WaitAsync(cancellationToken);
        }

        public INullByteFilesApi Files => throw new NotSupportedException();
        public INullByteBotnetApi Botnet => throw new NotSupportedException();
        public INullByteActivitiesApi Activities => throw new NotSupportedException();
        public INullByteCloudApi Cloud => throw new NotSupportedException();
        public INullBytePlayerApi Player => throw new NotSupportedException();
        public Task<HackResult> Hack(string hostname) => throw new NotSupportedException();
        public Task<GrowResult> Grow(string hostname) => throw new NotSupportedException();
        public Task<WeakenResult> Weaken(string hostname) => throw new NotSupportedException();
        public double GetServerMoneyAvailable(string hostname) => throw new NotSupportedException();
        public double GetServerMaxMoney(string hostname) => throw new NotSupportedException();
        public double GetServerSecurityLevel(string hostname) => throw new NotSupportedException();
        public double GetServerMinSecurityLevel(string hostname) => throw new NotSupportedException();
        public int GetServerRequiredHackingLevel(string hostname) => throw new NotSupportedException();
        public double GetServerMaxRam(string hostname) => throw new NotSupportedException();
        public double GetServerUsedRam(string hostname) => throw new NotSupportedException();
        public bool HasRootAccess(string hostname) => throw new NotSupportedException();
        public NukeResult Nuke(string hostname) => throw new NotSupportedException();
        public IReadOnlyList<string> Scan(string hostname) => throw new NotSupportedException();

        public Task WaitUntilSleepRequestedAsync() => sleepRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        public void CompleteSleep() => sleepCompletion.TrySetResult();

        public IReadOnlyList<ScriptProcessInfo> Ps(string hostname = "")
        {
            throw new NotImplementedException();
        }

        public Task<int> RunAsync(string filename, params string[] args)
        {
            throw new NotImplementedException();
        }

        public Task<int> ExecAsync(string filename, string hostname, params string[] args)
        {
            throw new NotImplementedException();
        }

        public bool Kill(int pid)
        {
            throw new NotImplementedException();
        }

        public int KillAll()
        {
            throw new NotImplementedException();
        }

        public bool ScriptRunning(string filename, string hostname = "")
        {
            throw new NotImplementedException();
        }

        public double GetScriptRam(string filename, string hostname = "")
        {
            throw new NotImplementedException();
        }
    }
}