using NullByte.Scripting.Compilation;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Compilation;

public sealed class IncludeScannerTests
{
    [Fact]
    public void Scan_WithAWellFormedDirective_ReturnsItsPathAndPosition()
    {
        var diagnostics = new List<ScriptDiagnostic>();

        var directives = IncludeScanner.Scan("#include \"lib/util.cx\"\nvar x = 1;", "main.cx", diagnostics);

        var directive = Assert.Single(directives);

        Assert.Equal("lib/util.cx", directive.Path);
        Assert.Equal((1, 1), (directive.Line, directive.Column));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Scan_WithATrailingComment_StillFindsThePath()
    {
        var directives = IncludeScanner.Scan("#include \"a.cx\" // helpers", "main.cx", []);

        Assert.Equal("a.cx", Assert.Single(directives).Path);
    }

    [Fact]
    public void Scan_WithAMalformedDirective_ReportsItAndDoesNotReturnIt()
    {
        var diagnostics = new List<ScriptDiagnostic>();

        var directives = IncludeScanner.Scan("#include missing", "main.cx", diagnostics);

        Assert.Empty(directives);

        var diagnostic = Assert.Single(diagnostics);

        Assert.Equal("NB1005", diagnostic.Id);
        Assert.Equal("main.cx", diagnostic.File);
    }

    [Fact]
    public void Blank_ReplacesTheDirectiveWithSpaces_AndKeepsEverythingElseInPlace()
    {
        const string source = "#include \"a.cx\"\nvar x = 1;";
        var directives = IncludeScanner.Scan(source, "main.cx", []);

        var blanked = IncludeScanner.Blank(source, directives);
        var lines = blanked.Split('\n');

        Assert.Equal(source.Length, blanked.Length);
        Assert.True(lines[0].All(c => c == ' '));
        Assert.Equal("var x = 1;", lines[1]);
    }

    [Fact]
    public void Blank_WithoutDirectives_ReturnsTheSourceUnchanged()
    {
        Assert.Equal("var x = 1;", IncludeScanner.Blank("var x = 1;", []));
    }

    [Fact]
    public void UsesIncludes_IsTrueOnlyForWellFormedDirectives()
    {
        Assert.True(SourceSnapshot.UsesIncludes("#include \"lib.cx\"\nvar x = 1;"));
        Assert.False(SourceSnapshot.UsesIncludes("var x = 1;"));
        Assert.False(SourceSnapshot.UsesIncludes("// #include \"lib.cx\""));
    }
}
