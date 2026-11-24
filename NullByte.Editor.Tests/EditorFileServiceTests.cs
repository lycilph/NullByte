
using System.Text;
using System.Text.Json;

namespace NullByte.Editor.Tests;

public sealed class EditorFileServiceTests
{
    [Fact]
    public async Task Diagnose_ReturnsFullDiagnosticRange()
    {
        using var editor = new EditorFixture();

        var response = await editor.SendAsync(
            "diagnose",
            "/home/test.cx",
            ("content", "nb.Print(missingValue);"));

        Assert.True(response["ok"]!.GetValue<bool>());
        var diagnostic = Assert.Single(
            response["result"]!["diagnostics"]!.AsArray(),
            item => item!["id"]!.GetValue<string>() == "CS0103");

        Assert.Equal(1, diagnostic!["line"]!.GetValue<int>());
        Assert.Equal(10, diagnostic["column"]!.GetValue<int>());
        Assert.Equal(1, diagnostic["endLine"]!.GetValue<int>());
        Assert.Equal(22, diagnostic["endColumn"]!.GetValue<int>());
    }

    [Fact]
    public async Task Stat_AndWriteFile_ExposeAndUpdateFileTimestamps()
    {
        using var editor = new EditorFixture();

        editor.Files.WriteFile("home", "test.cx", "initial content");

        var statResponse = await editor.SendAsync("stat", "/home/test.cx");
        using var statDocument = JsonDocument.Parse(statResponse.ToJsonString());

        var statResult = statDocument.RootElement.GetProperty("result");
        var createdBefore = statResult.GetProperty("created").GetInt64();
        var modifiedBefore = statResult.GetProperty("modified").GetInt64();

        var writeResponse = await editor.SendAsync(
            "writeFile",
            "/home/test.cx",
            ("content", "updated content"),
            ("create", false),
            ("overwrite", true));

        var updatedStatResponse = await editor.SendAsync("stat", "/home/test.cx");
        using var updatedStatDocument = JsonDocument.Parse(updatedStatResponse.ToJsonString());

        var updatedStat = updatedStatDocument.RootElement.GetProperty("result");

        var createdAfter = updatedStat.GetProperty("created").GetInt64();
        var modifiedAfter = updatedStat.GetProperty("modified").GetInt64();

        Assert.Equal("file", statResult.GetProperty("type").GetString());
        Assert.Equal(
            Encoding.UTF8.GetByteCount("initial content"),
            statResult.GetProperty("size").GetInt64());

        Assert.True(writeResponse["ok"]!.GetValue<bool>());
        Assert.False(writeResponse["result"]!["created"]!.GetValue<bool>());

        Assert.Equal(
            Encoding.UTF8.GetByteCount("updated content"),
            updatedStat.GetProperty("size").GetInt64());

        Assert.Equal(createdBefore, createdAfter);
        Assert.True(modifiedBefore >= createdBefore);
        Assert.True(modifiedAfter > modifiedBefore);
    }
}
