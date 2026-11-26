using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Core.Domain;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests;

internal sealed class ThrowingApiFactory : INullByteApiFactory
{
    public INullByteApi Create(
        IScriptOperations scriptOperations, ServerId processHostServerId, int threads, CancellationToken cancellationToken)
        => throw new NotSupportedException("These tests never get as far as running a script.");
}

public class ScriptServicePathTests
{
    private readonly GameState state;
    private readonly ScriptProcessManager processes = new();
    private readonly ScriptService service;

    public ScriptServicePathTests()
    {
        var network = new Network();
        network.AddServer(new Server(
            "home", maximumMoney: 0, startingMoney: 0, baseDifficulty: 1, minimumSecurity: 1,
            startingSecurity: 1, requiredHackingLevel: 1, growthParameter: 0, maximumRam: 8, hasRootAccess: true));

        state = new GameState(new Player("home", 0, 0), network);
        service = new ScriptService(
            state, new ScriptCompiler(), new ScriptRunner(new ScriptAssemblyLoader()),
            new ThrowingApiFactory(), processes);
    }

    [Fact]
    public async Task Start_reports_an_invalid_path_instead_of_throwing()
    {
        var result = await service.Start("home", "a//b", [], 1);

        Assert.False(result.Succeeded);
        Assert.StartsWith("Invalid file name 'a//b': ", result.FailureMessage);
    }

    [Fact]
    public async Task Start_names_directories_and_missing_files_by_their_normalized_path()
    {
        state.FileSystem.WriteFile("home", "tools/a.cx", "nb.GetHackingLevel();");

        Assert.Equal("tools is a directory.", (await service.Start("home", "/tools", [], 1)).FailureMessage);
        Assert.Equal("File not found: tools/nope.cx", (await service.Start("home", "/tools/nope.cx", [], 1)).FailureMessage);
    }

    [Fact]
    public void GetScriptRam_finds_a_script_however_its_path_is_spelled()
    {
        state.FileSystem.WriteFile("home", "tools/a.cx", "nb.GetHackingLevel();");

        var ram = service.GetScriptRam("tools/a.cx", "home");

        Assert.True(ram > 0);
        Assert.Equal(ram, service.GetScriptRam("/tools/a.cx", "home"));
        Assert.Equal(0.0, service.GetScriptRam("tools/nope.cx", "home"));
        Assert.Equal(0.0, service.GetScriptRam("a//b", "home"));
    }

    [Fact]
    public void ScriptRunning_treats_differently_spelled_paths_as_the_same_script()
    {
        processes.Create("tools/a.cx", state.Network.GetServer("home").Id, [], 1, 0);

        Assert.True(service.ScriptRunning("tools/a.cx", "home"));
        Assert.True(service.ScriptRunning("/tools/a.cx", "home"));
        Assert.False(service.ScriptRunning("TOOLS/a.cx", "home"));   // paths are case-sensitive
        Assert.False(service.ScriptRunning("tools/b.cx", "home"));
        Assert.False(service.ScriptRunning("a//b", "home"));
    }
}