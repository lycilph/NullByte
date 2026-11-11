using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Simulation;
using NullByte.Core.Threading;
using NullByte.Core.Time;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.ConsoleUI.Tests;

public sealed class RunCommandTests
{
    [Fact]
    public void RunCommand_IsRegistered()
    {
        var registry = CommandRegistryFactory.Create();

        Assert.True(registry.TryGet("run", out var command));
        Assert.NotNull(command);
        Assert.False(command!.Debug);
        Assert.Equal("run <script> [args...]", command.Usage);
    }

    [Fact]
    public async Task Run_WithoutScript_ShowsUsage()
    {
        var harness = new Harness();

        await harness.RunAsync("run");

        Assert.Contains("Usage: run <script> [args...]", harness.Output);
    }

    [Fact]
    public async Task Run_WithMissingScript_ShowsFileNotFound()
    {
        var harness = new Harness();

        await harness.RunAsync("run missing.cs");

        Assert.Contains("File not found: missing.cs", harness.Output);
    }

    [Fact]
    public async Task Run_WithValidScript_StartsScript()
    {
        var harness = new Harness();

        harness.WriteScript(
            "hello.cs",
            """
            nb.Print("Hello from script");
            """);

        await harness.RunAsync("run hello.cs");

        Assert.Contains("Started hello.cs.", harness.Output);
        Assert.Contains("Hello from script", harness.Output);
    }

    [Fact]
    public async Task Run_PassesArgumentsToScript()
    {
        var harness = new Harness();

        harness.WriteScript(
            "args.cs",
            """
            nb.Print($"Target: {args[0]}");
            nb.Print($"Mode: {args[1]}");
            """);

        await harness.RunAsync("run args.cs n00dles aggressive");

        Assert.Contains("Target: n00dles", harness.Output);
        Assert.Contains("Mode: aggressive", harness.Output);
    }

    [Fact]
    public async Task Run_WithQuotedArgument_PreservesArgument()
    {
        var harness = new Harness();

        harness.WriteScript(
            "args.cs",
            """
            nb.Print(args[0]);
            """);

        await harness.RunAsync("""run args.cs "hello world""");

        Assert.Contains("hello world", harness.Output);
    }

    [Fact]
    public async Task Run_WithCompilationError_PrintsDiagnostic()
    {
        var harness = new Harness();

        harness.WriteScript(
            "broken.cs",
            """
            nb.Print(missingValue);
            """);

        await harness.RunAsync("run broken.cs");

        Assert.Contains("broken.cs(1,", harness.Output);
        Assert.Contains("error CS0103", harness.Output);
        Assert.Contains("missingValue", harness.Output);
        Assert.DoesNotContain("Started broken.cs.", harness.Output);
    }

    [Fact]
    public async Task Run_WithCompilationError_DoesNotPrintGenericFailure()
    {
        var harness = new Harness();

        harness.WriteScript(
            "broken.cs",
            """
            nb.Print(missingValue);
            """);

        await harness.RunAsync("run broken.cs");

        Assert.Contains("error CS0103", harness.Output);
        Assert.DoesNotContain("Compilation failed", harness.Output);
    }

    [Fact]
    public async Task Run_WithActiveScript_ReturnsWithoutWaitingForCompletion()
    {
        var harness = new Harness();

        harness.WriteScript(
            "waiting.cs",
            """
        nb.Print("Before sleep");
        await nb.Sleep(TimeSpan.FromHours(1));
        nb.Print("After sleep");
        """);

        await harness.RunAsync("run waiting.cs");

        try
        {
            await harness.ScriptApi.WaitUntilSleepingAsync();

            Assert.Contains("Started waiting.cs.", harness.Output);
            Assert.Contains("Before sleep", harness.Output);
            Assert.DoesNotContain("After sleep", harness.Output);
            Assert.True(harness.ScriptService.IsScriptRunning);
        }
        finally
        {
            harness.ScriptApi.CompleteSleep();

            await WaitUntilAsync(() => !harness.ScriptService.IsScriptRunning);
        }

        Assert.Contains("After sleep", harness.Output);
    }

    [Fact]
    public async Task Run_WhileAnotherScriptIsRunning_ShowsFailure()
    {
        var harness = new Harness();

        harness.WriteScript(
            "waiting.cs",
            """
        await nb.Sleep(TimeSpan.FromHours(1));
        """);

        harness.WriteScript(
            "second.cs",
            """
        nb.Print("Second script");
        """);

        await harness.RunAsync("run waiting.cs");

        try
        {
            await harness.ScriptApi.WaitUntilSleepingAsync();

            await harness.RunAsync("run second.cs");

            Assert.Contains("A script is already running.", harness.Output);
            Assert.DoesNotContain("Second script", harness.Output);
            Assert.DoesNotContain("Started second.cs.", harness.Output);
        }
        finally
        {
            harness.ScriptApi.CompleteSleep();

            await WaitUntilAsync(() => !harness.ScriptService.IsScriptRunning);
        }
    }

    [Fact]
    public async Task Run_UsesCurrentServerFileSystem()
    {
        var harness = new Harness();

        harness.WriteScript(
            "local.cs",
            """
            nb.Print("Local script");
            """);

        await harness.RunAsync("run local.cs");

        Assert.Contains("Local script", harness.Output);
    }

    private sealed class Harness
    {
        private readonly CommandDispatcher dispatcher;
        private readonly CommandContext context;
        private readonly FakeTerminal terminal = new();

        public Harness()
        {
            var balance = new BalanceConfig();

            Home = new Server(
                id: "home",
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

            var gameLoop = new GameLoop(
                new RealGameClock(),
                new GameSynchronizationContext());

            var hackingFormulas =
                new HackingFormulas(balance);

            var botnetFormulas =
                new BotnetFormulas(balance);

            var serverActions = new ServerActions(
                GameState,
                gameLoop.Scheduler,
                hackingFormulas,
                new SeededRandomSource(1));

            var scriptOutput =
                new TerminalScriptOutput(terminal);

            ScriptApi = new TestNullByteApi(terminal);

            ScriptService = new ScriptService(
                GameState,
                new ScriptCompiler(),
                new ScriptRunner(
                    new ScriptAssemblyLoader()),
                () => ScriptApi);

            var registry =
                CommandRegistryFactory.Create();

            dispatcher =
                new CommandDispatcher(registry);

            context = new CommandContext(
                GameState,
                gameLoop,
                new NetworkService(GameState),
                serverActions,
                new BotnetService(
                    GameState,
                    botnetFormulas),
                ScriptService,
                hackingFormulas,
                botnetFormulas,
                registry,
                terminal);
        }

        public Server Home { get; }

        public GameState GameState { get; }

        public ScriptService ScriptService { get; }
        
        public TestNullByteApi ScriptApi { get; }

        public string Output => string.Join(Environment.NewLine, terminal.Lines);

        public void WriteScript(string fileName, string source)
        {
            GameState.FileSystem.WriteFile(Home.Id, fileName, source);
        }

        public Task RunAsync(string commandLine)
        {
            return dispatcher.DispatchAsync(commandLine, context);
        }
    }

    private sealed class FakeTerminal : IGameTerminal
    {
        public List<string> Lines { get; } = [];

        public CancellationToken StoppingToken =>
            CancellationToken.None;

        public void StartProgress(string message, Func<double> progress) {}

        public void StopProgress() {}

        public void SetPrompt(string prompt) {}

        public void SetInputEnabled(bool enabled) {}

        public async IAsyncEnumerable<string>
            ReadCommandsAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public void WriteLine()
        {
            Lines.Add(string.Empty);
        }

        public void WriteLine(string message)
        {
            Lines.Add(message);
        }

        public void Clear()
        {
            Lines.Clear();
        }
    }

    private sealed class TestNullByteApi : INullByteApi
    {
        private readonly IGameTerminal terminal;
        private readonly TaskCompletionSource sleepRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource sleepCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestNullByteApi(IGameTerminal terminal)
        {
            this.terminal = terminal;
        }

        public void Print(object? value)
        {
            terminal.WriteLine(value?.ToString() ?? string.Empty);
        }

        public Task Sleep(TimeSpan duration, CancellationToken cancellationToken = default)
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

        public Task<HackResult> Hack(string hostname, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<GrowResult> Grow(string hostname,CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WeakenResult> Weaken(string hostname, CancellationToken cancellationToken = default)
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