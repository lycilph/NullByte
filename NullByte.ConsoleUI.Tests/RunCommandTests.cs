using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Scripting.Runtime;
using NullByte.Shell;

namespace NullByte.ConsoleUI.Tests;

public sealed class RunCommandTests
{
    [Fact]
    public void RunCommand_IsRegistered()
    {
        var terminal = new FakeTerminal();
        var session = GameSession.Create(new GameSessionOptions() { Output = terminal });
        var hostRequests = new HostRequestMailbox();
        var registry = CommandSetup.Create(session, terminal, hostRequests);

        Assert.True(registry.TryGet("run", out var command));
        Assert.NotNull(command);
        Assert.False(command!.Debug);
        Assert.Equal("run <filename> [-t <threads>] [args...]", command.Usage);
    }

    [Fact]
    public async Task Run_WithoutScript_ShowsUsage()
    {
        var harness = new Harness();

        await harness.RunAsync("run");

        Assert.Contains("Usage: run <filename> [-t <threads>] [args...]", harness.Output);
    }

    [Fact]
    public async Task Run_WithMissingScript_ShowsFileNotFound()
    {
        var harness = new Harness();

        await harness.RunAsync("run missing.cx");

        Assert.Contains("File not found: missing.cx", harness.Output);
    }

    [Fact]
    public async Task Run_WithValidScript_StartsScript()
    {
        var harness = new Harness();

        harness.WriteScript(
            "hello.cx",
            """
            nb.Print("Hello from script");
            """);

        await harness.RunAsync("run hello.cx");

        Assert.Contains("Executing hello.cx.", harness.Output);
        Assert.Contains("Hello from script", harness.Output);
    }

    [Fact]
    public async Task Run_PassesArgumentsToScript()
    {
        var harness = new Harness();

        harness.WriteScript(
            "args.cx",
            """
            nb.Print($"Target: {args[0]}");
            nb.Print($"Mode: {args[1]}");
            """);

        await harness.RunAsync("run args.cx n00dles aggressive");

        Assert.Contains("Target: n00dles", harness.Output);
        Assert.Contains("Mode: aggressive", harness.Output);
    }

    [Fact]
    public async Task Run_WithQuotedArgument_PreservesArgument()
    {
        var harness = new Harness();

        harness.WriteScript(
            "args.cx",
            """
            nb.Print(args[0]);
            """);

        await harness.RunAsync("""run args.cx "hello world""");

        Assert.Contains("hello world", harness.Output);
    }

    [Fact]
    public async Task Run_WithCompilationError_PrintsDiagnostic()
    {
        var harness = new Harness();

        harness.WriteScript(
            "broken.cx",
            """
            nb.Print(missingValue);
            """);

        await harness.RunAsync("run broken.cx");

        Assert.Contains("broken.cx(1,", harness.Output);
        Assert.Contains("error CS0103", harness.Output);
        Assert.Contains("missingValue", harness.Output);
        Assert.DoesNotContain("Started broken.cx.", harness.Output);
    }

    [Fact]
    public async Task Run_WithCompilationError_DoesNotPrintGenericFailure()
    {
        var harness = new Harness();

        harness.WriteScript(
            "broken.cx",
            """
            nb.Print(missingValue);
            """);

        await harness.RunAsync("run broken.cx");

        Assert.Contains("error CS0103", harness.Output);
        Assert.DoesNotContain("Compilation failed", harness.Output);
    }

    [Fact]
    public async Task Run_WithActiveScript_ReturnsWithoutWaitingForCompletion()
    {
        var harness = new Harness();

        harness.WriteScript(
            "waiting.cx",
            """
        nb.Print("Before sleep");
        await nb.Sleep(TimeSpan.FromHours(1));
        nb.Print("After sleep");
        """);

        await harness.RunAsync("run waiting.cx");

        try
        {
            await harness.ScriptApi.WaitUntilSleepingAsync();

            Assert.Contains("Executing waiting.cx.", harness.Output);
            Assert.Contains("Before sleep", harness.Output);
            Assert.DoesNotContain("After sleep", harness.Output);
            Assert.True(harness.ScriptService.HasRunningProcesses);
        }
        finally
        {
            harness.ScriptApi.CompleteSleep();

            await WaitUntilAsync(() => !harness.ScriptService.HasRunningProcesses);
        }

        Assert.Contains("After sleep", harness.Output);
    }

    [Fact]
    public async Task Run_UsesCurrentServerFileSystem()
    {
        var harness = new Harness();

        harness.WriteScript(
            "local.cx",
            """
            nb.Print("Local script");
            """);

        await harness.RunAsync("run local.cx");

        Assert.Contains("Local script", harness.Output);
    }

    private sealed class Harness
    {
        private readonly CommandDispatcher dispatcher;
        private readonly FakeTerminal terminal = new();

        public Harness()
        {
            var balance = new BalanceConfig();

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

            GameState = new GameState(new Player("home"), network);

            ScriptApi = new TestNullByteApi(terminal);
            var apiFactory = new TestNullByteApiFactory(ScriptApi);

            var session = GameSession.Create(new GameSessionOptions() { Balance = balance, State = GameState, ApiFactory = apiFactory, Output = terminal });
            var hostRequests = new HostRequestMailbox();
            var registry = CommandSetup.Create(session, terminal, hostRequests);
            dispatcher = new CommandDispatcher(registry, terminal);

            ScriptService = session.Services.Scripts;
        }

        public Server Home { get; }

        public GameState GameState { get; }

        public ScriptService ScriptService { get; }

        public TestNullByteApi ScriptApi { get; }

        public string Output => string.Join(Environment.NewLine, terminal.Lines);

        public void WriteScript(string fileName, string source)
        {
            GameState.FileSystem.WriteFile(Home.Hostname, fileName, source);
        }

        public Task RunAsync(string commandLine)
        {
            return dispatcher.DispatchAsync(commandLine);
        }
    }

    private sealed class TestNullByteApiFactory(TestNullByteApi api) : INullByteApiFactory
    {
        public INullByteApi Create(IScriptOperations scriptOperations, string hostname, int threads, CancellationToken cancellationToken) => api;
    }

    private sealed class TestNullByteApi : INullByteApi
    {
        private readonly IGameTerminal terminal;
        private readonly TaskCompletionSource sleepRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource sleepCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken cancellationToken = new();

        public INullByteFilesApi Files => throw new NotImplementedException();
        public INullByteBotnetApi Botnet => throw new NotSupportedException();

        public TestNullByteApi(IGameTerminal terminal)
        {
            this.terminal = terminal;
        }

        public void Print(object? value)
        {
            terminal.WriteLine(value?.ToString() ?? string.Empty);
        }

        public Task Sleep(TimeSpan duration)
        {
            sleepRequested.TrySetResult();

            return sleepCompletion.Task.WaitAsync(cancellationToken);
        }

        public Task WaitUntilSleepingAsync()
        {
            return sleepRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void CompleteSleep()
        {
            sleepCompletion.TrySetResult();
        }

        public Task<HackResult> Hack(string hostname)
        {
            throw new NotSupportedException();
        }

        public Task<GrowResult> Grow(string hostname)
        {
            throw new NotSupportedException();
        }

        public Task<WeakenResult> Weaken(string hostname)
        {
            throw new NotSupportedException();
        }

        public double GetServerMoneyAvailable(string hostname)
        {
            throw new NotSupportedException();
        }

        public double GetServerMaxMoney(string hostname)
        {
            throw new NotSupportedException();
        }

        public double GetServerSecurityLevel(string hostname)
        {
            throw new NotSupportedException();
        }

        public double GetServerMinSecurityLevel(string hostname)
        {
            throw new NotSupportedException();
        }

        public double GetHackingLevel()
        {
            throw new NotSupportedException();
        }

        public double GetServerRequiredHackingLevel(string hostname)
        {
            throw new NotSupportedException();
        }

        public double GetServerMaxRam(string hostname)
        {
            throw new NotSupportedException();
        }

        public double GetServerUsedRam(string hostname)
        {
            throw new NotSupportedException();
        }

        public bool HasRootAccess(string hostname)
        {
            throw new NotSupportedException();
        }

        public NukeResult Nuke(string hostname)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<string> Scan(string hostname)
        {
            throw new NotSupportedException();
        }

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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}