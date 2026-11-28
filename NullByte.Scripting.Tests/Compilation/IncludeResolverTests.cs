using NullByte.Scripting.Compilation;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Compilation;

public sealed class IncludeResolverTests
{
    private static IncludeGraph Resolve(string root, Dictionary<string, string> files)
        => new IncludeResolver(new FakeSources(files)).Resolve("main.cx", root);

    [Fact]
    public void Resolve_WithoutIncludes_GivesAnEmptyGraph()
    {
        var graph = Resolve("nb.Print(\"hi\");", []);

        Assert.Empty(graph.Includes);
        Assert.Empty(graph.Diagnostics);
    }

    [Fact]
    public void Resolve_PutsDependenciesFirst_AndLoadsDiamondsOnce()
    {
        var graph = Resolve("#include \"a.cx\"\n#include \"b.cx\"\n", new()
        {
            ["a.cx"] = "#include \"common.cx\"\nclass A {}",
            ["b.cx"] = "#include \"/common.cx\"\nclass B {}",   // leading slash = same file
            ["common.cx"] = "class C {}"
        });

        Assert.Empty(graph.Diagnostics);
        Assert.Equal(["common.cx", "a.cx", "b.cx"], graph.Includes.Select(i => i.Path));
    }

    [Fact]
    public void Resolve_WithACycle_ReportsTheChain()
    {
        var graph = Resolve("#include \"a.cx\"", new()
        {
            ["a.cx"] = "#include \"b.cx\"",
            ["b.cx"] = "#include \"a.cx\""
        });

        var error = Assert.Single(graph.Diagnostics);

        Assert.Equal("NB1002", error.Id);
        Assert.Equal("b.cx", error.File);
        Assert.Contains("a.cx -> b.cx -> a.cx", error.Message);
    }

    [Fact]
    public void Resolve_WithAMissingInclude_PointsAtTheDirective()
    {
        var graph = Resolve("// header\n#include \"nope.cx\"", []);

        var error = Assert.Single(graph.Diagnostics);

        Assert.Equal("NB1001", error.Id);
        Assert.Equal((2, 1, "main.cx"), (error.Line, error.Column, error.File));
    }

    [Fact]
    public void Resolve_IgnoresIncludesInStringsAndComments()
    {
        var graph = Resolve("var s = \"#include \\\"x.cx\\\"\";\n// #include \"x.cx\"\n/* \n#include \"x.cx\"\n*/", []);

        Assert.Empty(graph.Diagnostics);
        Assert.Empty(graph.Includes);
    }

    [Fact]
    public void Resolve_WithAMalformedInclude_ReportsIt()
    {
        var graph = Resolve("#include lib.cx", []);

        Assert.Equal("NB1005", Assert.Single(graph.Diagnostics).Id);
    }

    [Fact]
    public void Resolve_WithATooDeepChain_ReportsIt()
    {
        var files = Enumerable.Range(1, 9).ToDictionary(i => $"f{i}.cx", i => $"#include \"f{i + 1}.cx\"");
        files["f10.cx"] = "class Last {}";

        var graph = Resolve("#include \"f1.cx\"", files);

        Assert.Contains(graph.Diagnostics, d => d.Id == "NB1003");
    }
}
