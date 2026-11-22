using NullByte.Scripting.Compilation;
using NullByte.Scripting.Tests.Compilation;

namespace NullByte.Scripting.Tests.Compilation;

public sealed class LibraryOnItsOwnTests
{
    private const string Util = """
        using NullByte.Core.Scripting;

        public static class Util
        {
            public static void Greet(INullByteApi nb, string name) => nb.Print($"hello {name}");
        }
        """;

    private static ScriptCompilationResult Compile(string source, Dictionary<string, string>? files = null)
        => new ScriptCompiler().Compile(source, "lib/util.cx", new FakeSources(files ?? []));

    [Fact]
    public void CompilesCleanWithNoWarnings()
    {
        var result = Compile(Util);
        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ErrorsAreReportedInTheLibraryItself()
    {
        var result = Compile(Util.Replace("nb.Print($\"hello {name}\")", "nb.Nope()"));

        var error = Assert.Single(result.Diagnostics, d => d.Id == "CS1061");
        Assert.Equal(("lib/util.cx", 5), (error.File, error.Line));
    }

    [Fact]
    public void ALibraryCanIncludeAnotherLibrary()
    {
        var result = Compile(
            "#include \"lib/other.cx\"\npublic static class Wrapper { public static int Two() => Other.One() + 1; }",
            new() { ["lib/other.cx"] = "public static class Other { public static int One() => 1; }" });

        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
    }
}