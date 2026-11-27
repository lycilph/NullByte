
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Core.Scripting;
using NullByte.Core.Simulation;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.LanguageService;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NullByte.Editor.Tests;

internal sealed class InlineGameThread : IGameThread
{
    public bool IsGameThread => true;
    public bool IsRunning => true;

    public Task InvokeAsync(Func<Task> callback) => callback();

    public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
}

internal sealed class FakeApiFactory : INullByteApiFactory
{
    public INullByteApi Create(IScriptOperations scriptOperations, ServerId processHostServerId, int threads, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

internal sealed class EditorFixture : IDisposable
{
    private readonly ScriptLanguageService languageService = new();
    private readonly EditorFileService service;

    public EditorFixture(IScriptService? scripts = null)
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));
        network.Connect("home", "n00dles");

        State = new GameState(new Player(network.GetServer("home").Id, 0, 0), network);
        NetworkService = new NetworkService(State, new GameScheduler(new FakeGameClock()), new NetworkFormulas(new BalanceConfig()));

        var scriptCompiler = new ScriptCompiler();
        var scriptLoader = new ScriptAssemblyLoader();
        var scriptRunner = new ScriptRunner(scriptLoader);
        var scriptProcessManager = new ScriptProcessManager();
        var apiFactory = new FakeApiFactory();

        var scriptService = scripts ?? new ScriptService(State, scriptCompiler, scriptRunner, apiFactory, scriptProcessManager);
        var backend = new EditorBackend(State, new InlineGameThread(), scriptService);

        service = new EditorFileService(
            () => backend,
            scriptCompiler,
            languageService);
    }

    public GameState State { get; }

    public NetworkService NetworkService { get; }

    public VirtualFileSystem Files => State.FileSystem;

    public async Task<JsonObject> SendAsync(
        string op,
        string path,
        params (string Name, object Value)[] extra)
    {
        var request = new JsonObject
        {
            ["id"] = 1,
            ["op"] = op,
            ["path"] = path
        };

        foreach (var (name, value) in extra)
            request[name] = JsonSerializer.SerializeToNode(value);

        var response = await service.HandleAsync(request.ToJsonString());

        return JsonNode.Parse(response)!.AsObject();
    }

    public void Dispose() => languageService.Dispose();

    private static Server NewServer(string hostname) => new(
        hostname,
        maximumMoney: 0,
        startingMoney: 0,
        baseDifficulty: 1,
        minimumSecurity: 1,
        startingSecurity: 1,
        requiredHackingLevel: 1,
        growthParameter: 0,
        maximumRam: 8);
}