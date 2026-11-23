
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Runtime;

namespace NullByte.Editor.Tests;

public sealed class EditorFileServiceRunTests
{
    [Fact]
    public async Task Run_StartsScriptOnRequestedServerWithArguments()
    {
        var scripts = new FakeScriptService(
            ScriptStartResult.Success(42, []));

        using var editor = new EditorFixture(scripts);

        var response = await editor.SendAsync(
            "run",
            "/home/test.cx",
            ("args", new[] { "foo", "bar" }));

        Assert.True(response["ok"]!.GetValue<bool>());
        Assert.Equal(42, response["result"]!["pid"]!.GetValue<int>());

        Assert.Equal("home", scripts.Hostname);
        Assert.Equal("test.cx", scripts.Filename);
        Assert.Equal(["foo", "bar"], scripts.Args);
        Assert.Equal(1, scripts.Threads);
    }

    [Fact]
    public async Task Run_CompilationFailure_ReturnsDiagnostics()
    {
        var diagnostics = new[]
        {
            new ScriptDiagnostic(
                "CS1002",
                "Expected ;",
                ScriptDiagnosticSeverity.Error,
                3,
                12,
                "test.cx")
        };

        var scripts = new FakeScriptService(
            ScriptStartResult.Failure(
                "Compilation failed for 'test.cx'.",
                diagnostics));

        using var editor = new EditorFixture(scripts);

        var response = await editor.SendAsync(
            "run",
            "/home/test.cx",
            ("args", Array.Empty<string>()));

        var result = response["result"]!.AsObject();

        Assert.False(result["started"]!.GetValue<bool>());
        Assert.Equal(0, result["pid"]!.GetValue<int>());
        Assert.Equal(
            "Compilation failed for 'test.cx'.",
            result["failureMessage"]!.GetValue<string>());

        var diagnostic = result["diagnostics"]![0]!;

        Assert.Equal("CS1002", diagnostic["id"]!.GetValue<string>());
        Assert.Equal("Expected ;", diagnostic["message"]!.GetValue<string>());
        Assert.Equal("Error", diagnostic["severity"]!.GetValue<string>());
        Assert.Equal(3, diagnostic["line"]!.GetValue<int>());
        Assert.Equal(12, diagnostic["column"]!.GetValue<int>());
        Assert.Equal("test.cx", diagnostic["file"]!.GetValue<string>());
    }

    [Fact]
    public async Task Run_InvalidArguments_ReturnsBadRequest()
    {
        var scripts = new FakeScriptService(
            ScriptStartResult.Success(42, []));

        using var editor = new EditorFixture(scripts);

        var response = await editor.SendAsync(
            "run",
            "/home/test.cx",
            ("args", "not-an-array"));

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Equal("BadRequest", response["error"]!["code"]!.GetValue<string>());
        Assert.Null(scripts.Hostname);
    }

    [Theory]
    [InlineData("/home")]
    [InlineData("/")]
    public async Task Run_InvalidScriptPath_ReturnsBadRequest(string path)
    {
        var scripts = new FakeScriptService(
            ScriptStartResult.Success(42, []));

        using var editor = new EditorFixture(scripts);

        var response = await editor.SendAsync(
            "run",
            path,
            ("args", Array.Empty<string>()));

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Equal("BadRequest", response["error"]!["code"]!.GetValue<string>());
        Assert.Null(scripts.Hostname);
    }

    private sealed class FakeScriptService(ScriptStartResult result) : IScriptService
    {
        public string? Hostname { get; private set; }
        public string? Filename { get; private set; }
        public IReadOnlyList<string>? Args { get; private set; }
        public int Threads { get; private set; }

        public Task<ScriptStartResult> Start(
            string hostname,
            string filename,
            IReadOnlyList<string> args,
            int threads = 1)
        {
            Hostname = hostname;
            Filename = filename;
            Args = args.ToArray();
            Threads = threads;

            return Task.FromResult(result);
        }
    }
}
