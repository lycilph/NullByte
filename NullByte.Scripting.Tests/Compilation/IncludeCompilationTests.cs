using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Compilation;

public sealed class IncludeCompilationTests
{
    private const string Util = """
        using NullByte.Core.Scripting;

        public static class Util
        {
            public static void Greet(INullByteApi nb, string name) => nb.Print($"hello {name}");
        }
        """;

    private static ScriptCompilationResult Compile(string main, Dictionary<string, string> files)
        => new ScriptCompiler().Compile(main, "main.cx", new FakeSources(files));

    [Fact]
    public void ScriptCanUseAnIncludedType()
    {
        var result = Compile("#include \"lib/util.cx\"\nUtil.Greet(nb, \"world\");", new() { ["lib/util.cx"] = Util });
        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
    }

    [Fact]
    public void ErrorInAnIncludeNamesThatFileAndLine()
    {
        var bad = "using System;\n\npublic static class Bad\n{\n    public static int F() => \"x\";\n}";
        var result = Compile("#include \"lib/bad.cx\"", new() { ["lib/bad.cx"] = bad });

        var error = Assert.Single(result.Diagnostics, d => d.Id == "CS0029");
        Assert.Equal(("lib/bad.cx", 5), (error.File, error.Line));
        Assert.Equal(5, error.EndLine);
        Assert.True(error.EndColumn > error.Column);
    }

    [Fact]
    public void IncludeLineDoesNotShiftLineNumbersInTheMainScript()
    {
        var result = Compile("#include \"lib/util.cx\"\nnb.Print(\"a\");\nint x = \"s\";", new() { ["lib/util.cx"] = Util });

        var error = Assert.Single(result.Diagnostics, d => d.Id == "CS0029");
        Assert.Equal(("main.cx", 3), (error.File, error.Line));
    }

    [Fact]
    public void MissingInclude_Fails()
    {
        var result = Compile("#include \"nope.cx\"", []);
        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1001");
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(1, diagnostic.Column);
        Assert.Equal(1, diagnostic.EndLine);
        Assert.Equal("#include \"nope.cx\"".Length + 1, diagnostic.EndColumn);
    }

    [Fact]
    public void MalformedInclude_HighlightsTheWholeDirective()
    {
        const string source = "#include missing";
        var result = Compile(source, []);

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1005");
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(1, diagnostic.Column);
        Assert.Equal(1, diagnostic.EndLine);
        Assert.Equal(source.Length + 1, diagnostic.EndColumn);
    }

    [Fact]
    public void IncludeCycle_HighlightsTheCycleDirective()
    {
        const string include = "#include \"main.cx\"";
        var result = Compile("#include \"lib/a.cx\"", new()
        {
            ["lib/a.cx"] = include
        });

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1002");
        Assert.Equal("lib/a.cx", diagnostic.File);
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(1, diagnostic.Column);
        Assert.Equal(1, diagnostic.EndLine);
        Assert.Equal(include.Length + 1, diagnostic.EndColumn);
    }

    [Fact]
    public void StatementsInAnInclude_AreRejected()
    {
        var result = Compile("#include \"lib/bad.cx\"", new() { ["lib/bad.cx"] = "nb.Print(\n    \"x\"\n);" });
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1006");

        Assert.Equal("lib/bad.cx", diagnostic.File);
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(3, diagnostic.EndLine);
        Assert.True(diagnostic.EndColumn > 1);
    }
}