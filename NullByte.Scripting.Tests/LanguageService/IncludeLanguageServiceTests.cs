using NullByte.Scripting.LanguageService;
using NullByte.Scripting.Tests.Compilation;

namespace NullByte.Scripting.Tests.LanguageService;

public sealed class IncludeLanguageServiceTests : IDisposable
{
    private const string Util = """
        using NullByte.Core.Scripting;

        public static class Util
        {
            /// <summary>Says hello.</summary>
            public static void Greet(INullByteApi nb, string name) => nb.Print($"hello {name}");
        }
        """;

    private readonly ScriptLanguageService service = new();
    private static readonly IncludeContext Context =
        new("main.cx", new FakeSources(new() { ["lib/util.cx"] = Util }));

    public void Dispose() => service.Dispose();

    private static int LineOf(string text, string fragment)
        => text.Split('\n').ToList().FindIndex(l => l.Contains(fragment)) + 1;

    [Fact]
    public async Task DefinitionOfAnIncludedMethodIsInTheLibrary()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.Greet(nb, \"x\");";
        var definition = await service.GetDefinitionAsync(
            source, source.IndexOf("Greet", StringComparison.Ordinal) + 1, TestContext.Current.CancellationToken, context: Context);

        Assert.Equal(new ScriptDefinition("lib/util.cx", LineOf(Util, "void Greet"), 24), definition);
    }

    [Fact]
    public async Task DefinitionInTheScriptItselfHasNoFile()
    {
        const string source = "var p = new Point();\nclass Point {}";
        var definition = await service.GetDefinitionAsync(source, source.IndexOf("Point", StringComparison.Ordinal) + 1, TestContext.Current.CancellationToken);

        Assert.Equal(2, definition!.Line);
        Assert.Null(definition.File);
    }

    [Fact]
    public async Task ApiMembersAndTheGeneratedWrapperHaveNoDefinition()
    {
        const string source = "nb.Player.Skills.Hacking.Level.ToString();";
        Assert.Null(await service.GetDefinitionAsync(source, source.IndexOf("Level", StringComparison.Ordinal) + 1, TestContext.Current.CancellationToken));
        Assert.Null(await service.GetDefinitionAsync(source, 1, TestContext.Current.CancellationToken));   // 'nb' is declared in the generated wrapper
    }

    [Fact]
    public async Task CompletesMembersOfAnIncludedType()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.";
        var items = await service.GetCompletionsAsync(source, source.Length, TestContext.Current.CancellationToken, context: Context);
        Assert.Contains(items, i => i.InsertText == "Greet");
    }

    [Fact]
    public async Task WithoutContext_TheIncludedTypeIsUnknown()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.";
        var items = await service.GetCompletionsAsync(source, source.Length, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(items, i => i.InsertText == "Greet");
    }

    [Fact]
    public async Task OffsetsStayCorrectBelowTheIncludeLine()
    {
        const string source = "#include \"lib/util.cx\"\nnb.";
        var items = await service.GetCompletionsAsync(source, source.Length, TestContext.Current.CancellationToken, context: Context);
        Assert.Contains(items, i => i.InsertText == "Player");
    }

    [Fact]
    public async Task HoverShowsTheIncludedMethod()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.Greet(nb, \"x\");";
        var hover = await service.GetHoverAsync(source, source.IndexOf("Greet", StringComparison.Ordinal) + 1, TestContext.Current.CancellationToken, context: Context);

        Assert.NotNull(hover);
        Assert.Contains("Greet", hover.Signature);
        Assert.Contains("Says hello", hover.Documentation);
    }

    [Fact]
    public async Task SignatureHelpPointsAtTheSecondParameter()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.Greet(nb, ";
        var help = await service.GetSignatureHelpAsync(source, source.Length, TestContext.Current.CancellationToken, context: Context);

        var signature = Assert.Single(help!.Signatures);
        Assert.Equal(1, signature.ActiveParameter);
    }
}