using NSubstitute;
using NullByte.ConsoleUI.Tests.Infrastructure;
using NullByte.Core.Scripting;
using NullByte.Scripting.Runtime;

namespace NullByte.ConsoleUI.Tests;

public sealed class RunCommandTests
{
    [Fact]
    public async Task Run_without_a_script_shows_the_usage()
    {
        var game = new ScriptGame();

        await game.Console.RunAsync("run");

        Assert.Contains("Usage: run <filename> [-t <threads>] [args...]", game.Output);
    }

    [Fact]
    public async Task Run_with_a_missing_script_shows_file_not_found()
    {
        var game = new ScriptGame();

        await game.Console.RunAsync("run missing.cx");

        Assert.Contains("File not found: missing.cx", game.Output);
    }

    [Fact]
    public async Task Run_with_a_valid_script_starts_the_script()
    {
        var game = new ScriptGame();
        game.WriteScript("hello.cx", """nb.Print("Hello from script");""");

        await game.Console.RunAsync("run hello.cx");

        Assert.Contains("Executing hello.cx.", game.Output);
        Assert.Contains("Hello from script", game.Output);
    }

    [Fact]
    public async Task Run_passes_the_arguments_to_the_script()
    {
        var game = new ScriptGame();
        game.WriteScript(
            "args.cx",
            """
            nb.Print($"Target: {args[0]}");
            nb.Print($"Mode: {args[1]}");
            """);

        await game.Console.RunAsync("run args.cx n00dles aggressive");

        Assert.Contains("Target: n00dles", game.Output);
        Assert.Contains("Mode: aggressive", game.Output);
    }

    [Fact]
    public async Task Run_keeps_a_quoted_argument_together()
    {
        var game = new ScriptGame();
        game.WriteScript("args.cx", "nb.Print(args[0]);");

        await game.Console.RunAsync("""run args.cx "hello world""");

        Assert.Contains("hello world", game.Output);
    }

    [Fact]
    public async Task A_compilation_error_prints_the_diagnostic_and_nothing_else()
    {
        var game = new ScriptGame();
        game.WriteScript("broken.cx", "nb.Print(missingValue);");

        await game.Console.RunAsync("run broken.cx");

        Assert.Contains("broken.cx(1,", game.Output);
        Assert.Contains("error CS0103", game.Output);
        Assert.Contains("missingValue", game.Output);
        Assert.DoesNotContain("Started broken.cx.", game.Output);
        Assert.DoesNotContain("Compilation failed", game.Output);
    }

    [Fact]
    public async Task Run_returns_without_waiting_for_a_script_that_is_still_active()
    {
        var game = new ScriptGame();
        game.WriteScript(
            "waiting.cx",
            """
            nb.Print("Before sleep");
            await nb.Sleep(TimeSpan.FromHours(1));
            nb.Print("After sleep");
            """);

        await game.Console.RunAsync("run waiting.cx");

        try
        {
            await game.WaitUntilSleepingAsync();

            Assert.Contains("Executing waiting.cx.", game.Output);
            Assert.Contains("Before sleep", game.Output);
            Assert.DoesNotContain("After sleep", game.Output);
            Assert.True(game.ScriptService.HasRunningProcesses);
        }
        finally
        {
            game.CompleteSleep();

            await WaitUntilAsync(() => !game.ScriptService.HasRunningProcesses);
        }

        Assert.Contains("After sleep", game.Output);
    }

    [Fact]
    public async Task Run_uses_the_file_system_of_the_current_server()
    {
        var game = new ScriptGame();
        game.WriteScript("local.cx", """nb.Print("Local script");""");

        await game.Console.RunAsync("run local.cx");

        Assert.Contains("Local script", game.Output);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    /// <summary>
    /// A console game whose scripts get a substitute for the script API: <c>nb.Print</c> writes to the terminal
    /// and <c>nb.Sleep</c> waits until the test lets it go.
    /// </summary>
    private sealed class ScriptGame
    {
        private readonly TaskCompletionSource sleepRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource sleepCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ScriptGame()
        {
            var terminal = new FakeTerminal();

            var api = Substitute.For<INullByteApi>();
            api.When(a => a.Print(Arg.Any<object?>()))
                .Do(call => terminal.WriteLine(call.Arg<object?>()?.ToString() ?? string.Empty));
            api.Sleep(Arg.Any<TimeSpan>()).Returns(_ =>
            {
                sleepRequested.TrySetResult();
                return sleepCompletion.Task;
            });

            var apiFactory = Substitute.For<INullByteApiFactory>();
            apiFactory
                .Create(Arg.Any<IScriptOperations>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(api);

            Console = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions { ApiFactory = apiFactory, Terminal = terminal });
            ScriptService = Console.Session.Services.Scripts;
        }

        public ConsoleTestHarness Console { get; }

        public ScriptService ScriptService { get; }

        public string Output => Console.Output;

        public void WriteScript(string fileName, string source) => Console.Files.WriteFile("home", fileName, source);

        public Task WaitUntilSleepingAsync() => sleepRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void CompleteSleep() => sleepCompletion.TrySetResult();
    }
}
