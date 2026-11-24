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
        Assert.Contains(result.Diagnostics, d => d.Id == "NB1001");
    }

    [Fact]
    public void StatementsInAnInclude_AreRejected()
    {
        var result = Compile("#include \"lib/bad.cx\"", new() { ["lib/bad.cx"] = "nb.Print(\"x\");" });
        Assert.Contains(result.Diagnostics, d => d is { Id: "NB1006", File: "lib/bad.cx" });
    }
}