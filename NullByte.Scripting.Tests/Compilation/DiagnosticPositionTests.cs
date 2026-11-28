using NullByte.Scripting.Compilation;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Compilation;

/// <summary>
/// Diagnostics must point at the player's own text, however the compiler wraps the script.
/// Lines and columns are 1-based.
/// </summary>
public sealed class DiagnosticPositionTests
{
    public static TheoryData<string, string, int, int, int, int> SemanticErrors => new()
    {
        // source, diagnostic, line, column, end line, end column
        { "var target = args[0];\nnb.Print(target);\nnb.Print(missingValue);", "CS0103", 3, 10, 3, 22 },
        { "using System.Globalization;\n\nvar target = args[0];\nnb.Print(missingValue);", "CS0103", 4, 10, 4, 22 },
    };

    [Theory]
    [MemberData(nameof(SemanticErrors))]
    public void Compile_WithSemanticError_ReportsTheRangeInThePlayersSource(
        string source, string diagnosticId, int line, int column, int endLine, int endColumn)
    {
        var result = TestScripts.Compile(source);

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == diagnosticId);

        Assert.Equal((line, column, endLine, endColumn), (diagnostic.Line, diagnostic.Column, diagnostic.EndLine, diagnostic.EndColumn));
    }

    [Fact]
    public void Compile_WithSyntaxError_ReportsThePlayersLine()
    {
        var result = TestScripts.Compile("var target = args[0];\n\nif (target ==)\n{\n    nb.Print(target);\n}");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, d => d.Severity == ScriptDiagnosticSeverity.Error && d.Line == 3);
    }

    [Fact]
    public void Compile_WithMissingToken_ReportsAnEmptyRange()
    {
        var result = TestScripts.Compile("var target = args[0]");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "CS1002");

        Assert.Equal(diagnostic.Line, diagnostic.EndLine);
        Assert.Equal(diagnostic.Column, diagnostic.EndColumn);
    }
}
