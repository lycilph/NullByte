using NSubstitute;
using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests.Infrastructure;

/// <summary>
/// Entry point for tests that need a running script service: a home server, a game state and a
/// <see cref="ScriptService"/> whose scripts talk to a <see cref="ScriptApiProbe"/>.
/// </summary>
internal static class ScriptTestSetup
{
    public static ScriptWorld CreateWorld(double homeRam = 8) => new(homeRam);
}

internal sealed class ScriptWorld
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public ScriptWorld(double homeRam)
    {
        Home = TestServers.Create("home", maximumRam: homeRam);

        var network = new Network();
        network.AddServer(Home);

        State = new GameState(new Player(Home.Id), network);
        State.Narrative.Mode = NarrativeProgressionMode.Freeplay;

        Probe = new ScriptApiProbe();

        var apiFactory = Substitute.For<INullByteApiFactory>();
        apiFactory
            .Create(Arg.Any<IScriptOperations>(), Arg.Any<ServerId>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Probe.Api);

        Processes = new ScriptProcessManager();
        Service = new ScriptService(
            State,
            new ScriptCompiler(),
            new ScriptRunner(new ScriptAssemblyLoader()),
            apiFactory,
            Processes);
    }

    public Server Home { get; }

    public GameState State { get; }

    public ScriptApiProbe Probe { get; }

    public ScriptProcessManager Processes { get; }

    public ScriptService Service { get; }

    public void WriteScript(string path, string source) => State.FileSystem.WriteFile(Home.Hostname, path, source);

    public Server AddServer(Server server)
    {
        State.Network.AddServer(server);

        return server;
    }

    /// <summary>Starts a script on the home server and returns the result, whatever it is.</summary>
    public Task<ScriptStartResult> StartAsync(string fileName, params string[] args)
        => Service.Start(Home.Hostname, fileName, args);

    /// <summary>Starts a script on the home server and asserts that it started.</summary>
    public async Task<ScriptProcess> StartSucceedingAsync(string fileName, params string[] args)
    {
        var result = await StartAsync(fileName, args);

        Assert.True(result.Succeeded, result.FailureMessage);

        return Process(result.Pid);
    }

    public ScriptProcess Process(int pid) => Service.Processes.Single(process => process.Pid == pid);

    /// <summary>Waits until the process has finished, including releasing its RAM.</summary>
    public Task FinishedAsync(ScriptProcess process)
        => process.Execution!.WaitAsync(Timeout, TestContext.Current.CancellationToken);
}
