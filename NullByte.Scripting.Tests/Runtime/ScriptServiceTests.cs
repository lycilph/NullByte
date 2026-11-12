using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests.Runtime;

public sealed class ScriptServiceTests
{
    [Fact]
    public async Task Start_WithExistingScript_ExecutesScript()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "hello.cs",
            """
            nb.Print("Hello from script");
            """);

        var result = fixture.Service.Start(fixture.Home.Hostname, "hello.cs", []);

        Assert.True(result.Succeeded, result.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["Hello from script"], fixture.Api.Output);
    }

    [Fact]
    public void Start_WithMissingScript_Fails()
    {
        var fixture = new Fixture();

        var result = fixture.Service.Start(
            fixture.Home.Hostname,
            "missing.cs",
            []);

        Assert.False(result.Succeeded);
        Assert.Contains("File not found", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing.cs", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Diagnostics);
        Assert.False(fixture.Service.IsScriptRunning);
    }

    [Fact]
    public void Start_WithCompilationError_ReturnsDiagnostics()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "broken.cs",
            """
            nb.Print(missingValue);
            """);

        var result = fixture.Service.Start(fixture.Home.Hostname, "broken.cs", []);

        Assert.False(result.Succeeded);
        Assert.Contains("Compilation failed", result.FailureMessage, StringComparison.OrdinalIgnoreCase);

        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "CS0103");
        Assert.Equal(ScriptDiagnosticSeverity.Error, diagnostic.Severity);

        Assert.Equal(1, diagnostic.Line);
        Assert.False(fixture.Service.IsScriptRunning);
    }

    [Fact]
    public async Task Start_PassesArgumentsToScript()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "args.cs",
            """
            nb.Print(args[0]);
            nb.Print(args[1]);
            """);

        var result = fixture.Service.Start(fixture.Home.Hostname, "args.cs", ["n00dles", "second"]);

        Assert.True(result.Succeeded, result.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["n00dles", "second"], fixture.Api.Output);
    }

    [Fact]
    public async Task Start_SnapshotsArguments()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "args.cs",
            """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            nb.Print(args[0]);
            """);

        var args = new List<string> { "n00dles" };
        var result = fixture.Service.Start(fixture.Home.Hostname, "args.cs", args);

        Assert.True(result.Succeeded, result.FailureMessage);

        await fixture.Api.WaitUntilSleepRequestedAsync();

        args[0] = "changed";

        fixture.Api.CompleteSleep();

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["n00dles"], fixture.Api.Output);
    }

    [Fact]
    public async Task Start_WithRunningScript_ReturnsImmediately()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "sleep.cs",
            """
            nb.Print("Before");
            await nb.Sleep(TimeSpan.FromSeconds(10));
            nb.Print("After");
            """);

        var result = fixture.Service.Start(fixture.Home.Hostname, "sleep.cs", []);

        Assert.True(result.Succeeded, result.FailureMessage);

        await fixture.Api.WaitUntilSleepRequestedAsync();

        Assert.True(fixture.Service.IsScriptRunning);
        Assert.Equal(["Before"], fixture.Api.Output);

        fixture.Api.CompleteSleep();

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["Before", "After"], fixture.Api.Output);
    }

    [Fact]
    public async Task Start_WhileAnotherScriptIsRunning_Fails()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "first.cs",
            """
            await nb.Sleep(TimeSpan.FromSeconds(10));
            """);

        fixture.WriteScript(
            "second.cs",
            """
            nb.Print("Second");
            """);

        var firstResult = fixture.Service.Start(fixture.Home.Hostname, "first.cs",[]);

        Assert.True(firstResult.Succeeded, firstResult.FailureMessage);

        await fixture.Api.WaitUntilSleepRequestedAsync();

        var secondResult = fixture.Service.Start(fixture.Home.Hostname, "second.cs", []);

        Assert.False(secondResult.Succeeded);
        Assert.Contains("already running", secondResult.FailureMessage,StringComparison.OrdinalIgnoreCase);

        fixture.Api.CompleteSleep();

        await fixture.WaitForCompletionAsync();
    }

    [Fact]
    public async Task CompletedScript_AllowsAnotherScriptToStart()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "first.cs",
            """
            nb.Print("First");
            """);

        fixture.WriteScript(
            "second.cs",
            """
            nb.Print("Second");
            """);

        var firstResult = fixture.Service.Start(fixture.Home.Hostname, "first.cs", []);

        Assert.True(firstResult.Succeeded, firstResult.FailureMessage);

        await fixture.WaitForCompletionAsync();

        var secondResult = fixture.Service.Start(fixture.Home.Hostname, "second.cs", []);

        Assert.True(secondResult.Succeeded, secondResult.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Equal(["First", "Second"], fixture.Api.Output);
    }

    [Fact]
    public async Task CompletedScript_RaisesScriptCompleted()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "complete.cs",
            """
            nb.Print("Finished");
            """);

        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Service.ScriptCompleted += () => completed.TrySetResult();

        var result = fixture.Service.Start(fixture.Home.Hostname,"complete.cs",[]);

        Assert.True(result.Succeeded, result.FailureMessage);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(fixture.Service.IsScriptRunning);
    }

    [Fact]
    public async Task FailedScript_RaisesScriptFailed()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "failure.cs",
            """
            throw new InvalidOperationException("Script failed.");
            """);

        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Service.ScriptFailed += exception => failed.TrySetResult(exception);

        var result = fixture.Service.Start(fixture.Home.Hostname, "failure.cs", []);

        Assert.True(result.Succeeded, result.FailureMessage);

        var exception = await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("Script failed.",exception.Message);

        Assert.False(fixture.Service.IsScriptRunning);
    }

    [Fact]
    public async Task FailedScript_AllowsAnotherScriptToStart()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "failure.cs",
            """
            throw new InvalidOperationException("Boom");
            """);

        fixture.WriteScript(
            "recovery.cs",
            """
            nb.Print("Recovered");
            """);

        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Service.ScriptFailed += _ => failed.TrySetResult();

        var firstResult = fixture.Service.Start(fixture.Home.Hostname, "failure.cs", []);

        Assert.True(firstResult.Succeeded, firstResult.FailureMessage);

        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondResult = fixture.Service.Start(fixture.Home.Hostname, "recovery.cs", []);

        Assert.True(secondResult.Succeeded, secondResult.FailureMessage);

        await fixture.WaitForCompletionAsync();

        Assert.Contains("Recovered", fixture.Api.Output);
    }

    [Fact]
    public void Start_PreservesCompilerWarnings()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "warning.cs",
            """
            var unused = 42;
            nb.Print("Running");
            """);

        var result = fixture.Service.Start(fixture.Home.Hostname, "warning.cs", []);

        Assert.True(result.Succeeded, result.FailureMessage);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Start_WithInvalidServer_Throws()
    {
        var fixture = new Fixture();

        Assert.Throws<KeyNotFoundException>(
            () => fixture.Service.Start("missing-server", "script.cs", []));
    }

    [Fact]
    public void Start_WithInvalidFileName_Throws()
    {
        var fixture = new Fixture();

        Assert.Throws<ArgumentException>(
            () => fixture.Service.Start(fixture.Home.Hostname, "", []));
    }

    [Fact]
    public void Start_WithNullArguments_Throws()
    {
        var fixture = new Fixture();

        fixture.WriteScript(
            "script.cs",
            """
            nb.Print("Hello");
            """);

        Assert.Throws<ArgumentNullException>(
            () => fixture.Service.Start(fixture.Home.Hostname, "script.cs", null!));
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
                minimumSecurity: 1,
                startingSecurity: 1,
                requiredHackingLevel: 1,
                growthParameter: 0,
                maximumRam: 8,
                hasRootAccess: true);

            var network = new Network();
            network.AddServer(Home);

            GameState = new GameState(
                new Player("home"),
                network);

            Api = new TestNullByteApi();

            var compiler = new ScriptCompiler();
            var loader = new ScriptAssemblyLoader();
            var runner = new ScriptRunner(loader);

            Service = new ScriptService(
                GameState,
                compiler,
                runner,
                () => Api);

            Service.ScriptCompleted += () => scriptFinished.TrySetResult();

            Service.ScriptFailed += _ => scriptFinished.TrySetResult();
        }

        public Server Home { get; }

        public GameState GameState { get; }

        public TestNullByteApi Api { get; }

        public ScriptService Service { get; }

        public void WriteScript(string fileName, string source)
        {
            GameState.FileSystem.WriteFile(Home.Hostname, fileName,source);
        }

        public Task WaitForCompletionAsync()
        {
            return scriptFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class TestNullByteApi : INullByteApi
    {
        private readonly TaskCompletionSource sleepRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource sleepCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Output { get; } = [];

        public void Print(object? value) => Output.Add(value?.ToString() ?? string.Empty);

        public Task Sleep(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            sleepRequested.TrySetResult();
            return sleepCompletion.Task.WaitAsync(cancellationToken);
        }

        public Task<HackResult> Hack(string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GrowResult> Grow(string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WeakenResult> Weaken(string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public double GetServerMoneyAvailable(string hostname) => throw new NotSupportedException();
        public double GetServerMaxMoney(string hostname) => throw new NotSupportedException();
        public double GetServerSecurityLevel(string hostname) => throw new NotSupportedException();
        public double GetServerMinSecurityLevel(string hostname) => throw new NotSupportedException();

        public Task WaitUntilSleepRequestedAsync() => sleepRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        public void CompleteSleep() => sleepCompletion.TrySetResult();
    }
}