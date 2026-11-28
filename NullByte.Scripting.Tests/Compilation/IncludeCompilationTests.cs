using NullByte.Scripting.Compilation;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Compilation;

/// <summary>Compiling scripts and libraries that #include other files.</summary>
public sealed class IncludeCompilationTests
{
    private const string Util = """
        using NullByte.Core.Scripting;

        public static class Util
        {
            public static void Greet(INullByteApi nb, string name) => nb.Print($"hello {name}");
        }
        """;

    private static ScriptCompilationResult Compile(string source, Dictionary<string, string> files, string fileName = "main.cx")
        => TestScripts.Compile(source, fileName, files);

    [Fact]
    public void Compile_WithAnIncludedType_LetsTheScriptUseIt()
    {
        var result = Compile("#include \"lib/util.cx\"\nUtil.Greet(nb, \"world\");", new() { ["lib/util.cx"] = Util });

        CompileAssert.Succeeds(result);
    }

    [Fact]
    public void Compile_WithAnErrorInAnInclude_NamesThatFileAndLine()
    {
        var bad = "using System;\n\npublic static class Bad\n{\n    public static int F() => \"x\";\n}";

        var result = Compile("#include \"lib/bad.cx\"", new() { ["lib/bad.cx"] = bad });

        var error = Assert.Single(result.Diagnostics, d => d.Id == "CS0029");

        Assert.Equal(("lib/bad.cx", 5), (error.File, error.Line));
        Assert.Equal(5, error.EndLine);
        Assert.True(error.EndColumn > error.Column);
    }

    [Fact]
    public void Compile_WithAnInclude_DoesNotShiftLineNumbersInTheMainScript()
    {
        var result = Compile("#include \"lib/util.cx\"\nnb.Print(\"a\");\nint x = \"s\";", new() { ["lib/util.cx"] = Util });

        var error = Assert.Single(result.Diagnostics, d => d.Id == "CS0029");

        Assert.Equal(("main.cx", 3), (error.File, error.Line));
    }

    [Fact]
    public void Compile_WithAMissingInclude_FailsAndHighlightsTheDirective()
    {
        var result = Compile("#include \"nope.cx\"", []);

        Assert.False(result.Succeeded);

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1001");

        Assert.Equal((1, 1, 1), (diagnostic.Line, diagnostic.Column, diagnostic.EndLine));
        Assert.Equal("#include \"nope.cx\"".Length + 1, diagnostic.EndColumn);
    }

    [Fact]
    public void Compile_WithAMalformedInclude_HighlightsTheWholeDirective()
    {
        const string source = "#include missing";

        var result = Compile(source, []);

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1005");

        Assert.Equal((1, 1, 1), (diagnostic.Line, diagnostic.Column, diagnostic.EndLine));
        Assert.Equal(source.Length + 1, diagnostic.EndColumn);
    }

    [Fact]
    public void Compile_WithAnIncludeCycle_HighlightsTheCycleDirective()
    {
        const string include = "#include \"main.cx\"";

        var result = Compile("#include \"lib/a.cx\"", new() { ["lib/a.cx"] = include });

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1002");

        Assert.Equal("lib/a.cx", diagnostic.File);
        Assert.Equal((1, 1, 1), (diagnostic.Line, diagnostic.Column, diagnostic.EndLine));
        Assert.Equal(include.Length + 1, diagnostic.EndColumn);
    }

    [Fact]
    public void Compile_WithStatementsInAnInclude_RejectsThem()
    {
        var result = Compile("#include \"lib/bad.cx\"", new() { ["lib/bad.cx"] = "nb.Print(\n    \"x\"\n);" });

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "NB1006");

        Assert.Equal("lib/bad.cx", diagnostic.File);
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(3, diagnostic.EndLine);
        Assert.True(diagnostic.EndColumn > 1);
    }

    // A library compiled on its own, which is what the editor does when a library file is open.

    [Fact]
    public void Compile_OfALibraryOnItsOwn_IsCleanWithNoWarnings()
    {
        var result = Compile(Util, [], "lib/util.cx");

        CompileAssert.Succeeds(result);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compile_OfALibraryOnItsOwn_ReportsErrorsInTheLibraryItself()
    {
        var result = Compile(Util.Replace("nb.Print($\"hello {name}\")", "nb.Nope()"), [], "lib/util.cx");

        var error = Assert.Single(result.Diagnostics, d => d.Id == "CS1061");

        Assert.Equal(("lib/util.cx", 5), (error.File, error.Line));
    }

    [Fact]
    public void Compile_OfALibraryOnItsOwn_MayIncludeAnotherLibrary()
    {
        var result = Compile(
            "#include \"lib/other.cx\"\npublic static class Wrapper { public static int Two() => Other.One() + 1; }",
            new() { ["lib/other.cx"] = "public static class Other { public static int One() => 1; }" },
            "lib/util.cx");

        CompileAssert.Succeeds(result);
    }
}
