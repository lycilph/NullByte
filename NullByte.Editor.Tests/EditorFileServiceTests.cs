using System.Text.Json;
using NullByte.Core.Domain;
using NullByte.Core.Simulation;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.LanguageService;

namespace NullByte.Editor.Tests;

public sealed class EditorFileServiceTests
{
    [Fact]
    public async Task Stat_AndWriteFile_ExposeAndUpdateFileTimestamps()
    {
        // Arrange
        var network = new Network();
        network.AddServer(new Server(
            hostname: "home",
            maximumMoney: 0,
            startingMoney: 0,
            baseDifficulty: 5,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter: 0,
            maximumRam: 8,
            hasRootAccess: true));

        var player = new Player("home", startingMoney: 0);
        var state = new GameState(player, network);

        state.FileSystem.WriteFile("home", "test.cx", "initial content");

        var backend = new EditorBackend(state, new ImmediateGameThread());

        using var languageService = new ScriptLanguageService();
        var service = new EditorFileService(
            () => backend,
            new ScriptCompiler(),
            languageService);

        // Act: read the initial timestamps through the editor protocol.
        var statResponse = await service.HandleAsync("""{"id":1,"op":"stat","path":"home/test.cx"}""");

        using var statDocument = JsonDocument.Parse(statResponse);
        var statResult = statDocument.RootElement.GetProperty("result");

        var createdBefore = statResult.GetProperty("created").GetInt64();
        var modifiedBefore = statResult.GetProperty("modified").GetInt64();

        // Act: overwrite the file through the editor protocol.
        var writeResponse = await service.HandleAsync(
            """{"id":2,"op":"writeFile","path":"home/test.cx","content":"updated content","create":false,"overwrite":true}""");

        using var writeDocument = JsonDocument.Parse(writeResponse);

        // Read the timestamps again.
        var updatedStatResponse = await service.HandleAsync(
            """{"id":3,"op":"stat","path":"home/test.cx"}""");

        using var updatedStatDocument = JsonDocument.Parse(updatedStatResponse);
        var updatedStat = updatedStatDocument.RootElement.GetProperty("result");

        var createdAfter = updatedStat.GetProperty("created").GetInt64();
        var modifiedAfter = updatedStat.GetProperty("modified").GetInt64();

        // Assert
        Assert.True(statDocument.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("file", statResult.GetProperty("type").GetString());
        Assert.Equal(
            System.Text.Encoding.UTF8.GetByteCount("initial content"),
            statResult.GetProperty("size").GetInt64());

        Assert.True(writeDocument.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(writeDocument.RootElement.GetProperty("result")
            .GetProperty("created").GetBoolean());

        Assert.True(updatedStatDocument.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(
            System.Text.Encoding.UTF8.GetByteCount("updated content"),
            updatedStat.GetProperty("size").GetInt64());

        Assert.Equal(createdBefore, createdAfter);
        Assert.True(modifiedBefore >= createdBefore);
        Assert.True(modifiedAfter > modifiedBefore);
    }

    private sealed class ImmediateGameThread : IGameThread
    {
        public bool IsGameThread => true;
        public bool IsRunning => true;

        public Task InvokeAsync(Func<Task> callback) => callback();

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }
}
