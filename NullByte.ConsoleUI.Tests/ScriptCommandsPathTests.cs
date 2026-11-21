using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Commands.Groups;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.ConsoleUI.Tests;

internal sealed class ThrowingApiFactory : INullByteApiFactory
{
    public INullByteApi Create(
        IScriptOperations scriptOperations, string processHostname, int threads, CancellationToken cancellationToken)
        => throw new NotSupportedException("These tests never get as far as running a script.");
}

internal sealed class ScriptCommandsFixture
{
    private readonly RecordingTerminal terminal = new();
    private readonly CommandDispatcher dispatcher;

    public ScriptCommandsFixture()
    {
        var network = new Network();
        network.AddServer(new Server(
            "home", maximumMoney: 0, startingMoney: 0, baseDifficulty: 1, minimumSecurity: 1,
            startingSecurity: 1, requiredHackingLevel: 1, growthParameter: 0, maximumRam: 8, hasRootAccess: true));

        var state = new GameState(new Player("home", 0, 0), network);
        var networkService = new NetworkService(
            state, new GameScheduler(new FakeGameClock()), new NetworkFormulas(new BalanceConfig()));
        var scriptService = new ScriptService(
            state, new ScriptCompiler(), new ScriptRunner(new ScriptAssemblyLoader()),
            new ThrowingApiFactory(), new ScriptProcessManager());
        var workingDirectory = new WorkingDirectory(state);

        Files = state.FileSystem;

        var registry = new CommandRegistry();
        new FileCommands(Files, networkService, workingDirectory, terminal).Register(registry);   // for cd
        new ScriptCommands(networkService, scriptService, workingDirectory, terminal).Register(registry);
        dispatcher = new CommandDispatcher(registry, terminal);
    }

    public VirtualFileSystem Files { get; }

    public async Task<IReadOnlyList<string>> RunAsync(string commandLine)
    {
        terminal.Lines.Clear();
        await dispatcher.DispatchAsync(commandLine);
        return terminal.Lines.ToArray();
    }
}

public class ScriptCommandsPathTests
{
    [Fact]
    public async Task Mem_finds_the_script_however_its_path_is_spelled()
    {
        var game = new ScriptCommandsFixture();
        game.Files.WriteFile("home", "tools/a.cx", "nb.GetHackingLevel();");
        await game.RunAsync("cd tools");

        var relative = await game.RunAsync("mem a.cx");

        var line = Assert.Single(relative);
        Assert.StartsWith("This script requires", line);
        Assert.DoesNotContain("requires 0.00GB", line);   // 0 would mean it was not found

        Assert.Equal(relative, await game.RunAsync("mem /tools/a.cx"));
        Assert.Equal(relative, await game.RunAsync("mem ../tools/a.cx"));

        await game.RunAsync("cd /");
        Assert.Equal(relative, await game.RunAsync("mem tools/a.cx"));
    }

    [Fact]
    public async Task Run_reports_missing_files_and_directories_by_their_full_path()
    {
        var game = new ScriptCommandsFixture();
        game.Files.CreateDirectory("home", "tools/sub");
        await game.RunAsync("cd tools");

        Assert.Equal(
            new[] { "Executing tools/nope.cx...", "File not found: tools/nope.cx" },
            await game.RunAsync("run nope.cx"));

        Assert.Equal(
            new[] { "Executing tools/sub...", "tools/sub is a directory." },
            await game.RunAsync("run sub"));
    }

    [Fact]
    public async Task Run_and_mem_refuse_a_bad_path_before_anything_else()
    {
        var game = new ScriptCommandsFixture();
        await game.RunAsync("mkdir tools");   // for the cd below, not needed: just keeps the game non-empty
        await game.RunAsync("cd tools");

        Assert.StartsWith("Invalid file name: Path segments must be simple names", Assert.Single(await game.RunAsync("run a//b")));
        Assert.Equal(new[] { "Invalid file name: A file path cannot be empty." }, await game.RunAsync("run .."));
        Assert.Equal(new[] { "Invalid file name: A file path cannot be empty." }, await game.RunAsync("mem .."));
    }
}