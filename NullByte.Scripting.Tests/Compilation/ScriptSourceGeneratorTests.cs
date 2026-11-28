using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Compilation;

/// <summary>
/// The generated wrapper keeps the player's text as it is, so every offset in the player's file can be
/// mapped to an offset in the generated source (the language service relies on this).
/// </summary>
public sealed class ScriptSourceGeneratorTests
{
    [Fact]
    public void GenerateMapped_WrapsStatementsInTheGeneratedScriptClass()
    {
        var (_, generated) = Generate("nb.Print(\"hi\");");

        Assert.Contains($"class {ScriptSourceGenerator.TypeName}", generated.Text, StringComparison.Ordinal);
        Assert.Contains("nb.Print(\"hi\");", generated.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMapped_MapsBodyOffsetsOntoTheSameTextInTheGeneratedSource()
    {
        var (source, generated) = Generate("var x = 1;\nnb.Print(x);");

        var offset = source.IndexOf("nb.Print", StringComparison.Ordinal);

        Assert.Equal("nb.Print", generated.Text.Substring(generated.ToGenerated(offset), "nb.Print".Length));
    }

    [Fact]
    public void GenerateMapped_KeepsUsingsAheadOfTheClass_AndStillMapsTheBody()
    {
        var (source, generated) = Generate("using System.Text;\nvar x = 1;");

        Assert.True(
            generated.Text.IndexOf("using System.Text;", StringComparison.Ordinal) <
            generated.Text.IndexOf("public sealed class", StringComparison.Ordinal));

        var offset = source.IndexOf("var x", StringComparison.Ordinal);

        Assert.Equal("var x", generated.Text.Substring(generated.ToGenerated(offset), "var x".Length));
    }

    [Fact]
    public void GenerateMapped_PutsTypeDeclarationsAfterTheClass_AndStillMapsThem()
    {
        var (source, generated) = Generate("var p = new Point();\nclass Point {}");

        var declarationInGenerated = generated.Text.IndexOf("class Point", StringComparison.Ordinal);
        var endOfClass = generated.Text.IndexOf("#pragma warning restore", StringComparison.Ordinal);

        Assert.True(declarationInGenerated > endOfClass);

        var offset = source.IndexOf("class Point", StringComparison.Ordinal);

        Assert.Equal(declarationInGenerated, generated.ToGenerated(offset));
    }

    [Fact]
    public void ToGenerated_AtTheEndOfTheSource_MapsToTheEndOfTheLastSegment()
    {
        const string text = "nb.Print(x);";
        var (source, generated) = Generate(text);

        var endInGenerated = generated.Text.IndexOf(text, StringComparison.Ordinal) + text.Length;

        Assert.Equal(endInGenerated, generated.ToGenerated(source.Length));
    }

    private static (string Source, ScriptSourceGenerator.GeneratedScript Generated) Generate(string source)
    {
        var text = SourceText.From(source, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(text, ScriptCompiler.ParseOptions);

        return (source, ScriptSourceGenerator.GenerateMapped(text, tree.GetCompilationUnitRoot(), "script.cx"));
    }
}
