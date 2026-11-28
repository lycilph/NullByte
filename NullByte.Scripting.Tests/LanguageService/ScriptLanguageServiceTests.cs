using NullByte.Scripting.LanguageService;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.LanguageService;

/// <summary>
/// Completion, hover, signature help and go-to-definition. One service is shared by all the tests in this
/// class: it is expensive to create and each request works on its own immutable solution.
/// </summary>
public sealed class ScriptLanguageServiceTests(ScriptLanguageServiceTests.ServiceFixture fixture)
    : IClassFixture<ScriptLanguageServiceTests.ServiceFixture>
{
    public sealed class ServiceFixture : IDisposable
    {
        public ScriptLanguageService Service { get; } = new();

        public void Dispose() => Service.Dispose();
    }

    private const string Util = """
        using NullByte.Core.Scripting;

        public static class Util
        {
            /// <summary>Says hello.</summary>
            public static void Greet(INullByteApi nb, string name) => nb.Print($"hello {name}");
        }
        """;

    private static readonly IncludeContext Context =
        new("main.cx", new FakeSources(new() { ["lib/util.cx"] = Util }));

    private ScriptLanguageService Service => fixture.Service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static int LineOf(string text, string fragment)
        => text.Split('\n').ToList().FindIndex(l => l.Contains(fragment)) + 1;

    private static int OffsetOf(string source, string fragment)
        => source.IndexOf(fragment, StringComparison.Ordinal) + 1;

    // Without includes

    [Fact]
    public async Task WarmUpAsync_Completes()
    {
        await Service.WarmUpAsync();
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterNb_ListsThePlayerApi()
    {
        const string source = "nb.";

        var items = await Service.GetCompletionsAsync(source, source.Length, Token);

        Assert.Contains(items, i => i.InsertText == "Player");
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterPlayerSkills_ListsHacking()
    {
        const string source = "nb.Player.Skills.";

        var items = await Service.GetCompletionsAsync(source, source.Length, Token);

        Assert.Contains(items, i => i.InsertText == "Hacking");
    }

    [Fact]
    public async Task GetHoverAsync_OnAnApiMember_ShowsItsSignature()
    {
        const string source = "nb.Player.Skills.Hacking.Level";

        var hover = await Service.GetHoverAsync(source, OffsetOf(source, "Level"), Token);

        Assert.NotNull(hover);
        Assert.Contains("Level", hover.Signature);
    }

    [Fact]
    public async Task GetSignatureHelpAsync_InsideAnApiCall_ShowsTheFirstParameter()
    {
        const string source = "nb.Hack(";

        var help = await Service.GetSignatureHelpAsync(source, source.Length, Token);

        Assert.NotNull(help);
        Assert.Equal(0, help.Signatures.First().ActiveParameter);
    }

    [Fact]
    public async Task GetDefinitionAsync_InTheScriptItself_HasNoFile()
    {
        const string source = "var p = new Point();\nclass Point {}";

        var definition = await Service.GetDefinitionAsync(source, OffsetOf(source, "Point"), Token);

        Assert.Equal(2, definition!.Line);
        Assert.Null(definition.File);
    }

    [Fact]
    public async Task GetDefinitionAsync_OfApiMembersAndTheGeneratedWrapper_IsNull()
    {
        const string source = "nb.Player.Skills.Hacking.Level.ToString();";

        Assert.Null(await Service.GetDefinitionAsync(source, OffsetOf(source, "Level"), Token));
        Assert.Null(await Service.GetDefinitionAsync(source, 1, Token));   // 'nb' is declared in the generated wrapper
    }

    // With includes

    [Fact]
    public async Task GetDefinitionAsync_OfAnIncludedMethod_IsInTheLibrary()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.Greet(nb, \"x\");";

        var definition = await Service.GetDefinitionAsync(source, OffsetOf(source, "Greet"), Token, context: Context);

        Assert.Equal(new ScriptDefinition("lib/util.cx", LineOf(Util, "void Greet"), 24), definition);
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterAnIncludedType_ListsItsMembers()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.";

        var items = await Service.GetCompletionsAsync(source, source.Length, Token, context: Context);

        Assert.Contains(items, i => i.InsertText == "Greet");
    }

    [Fact]
    public async Task GetCompletionsAsync_WithoutIncludeContext_DoesNotKnowTheIncludedType()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.";

        var items = await Service.GetCompletionsAsync(source, source.Length, Token);

        Assert.DoesNotContain(items, i => i.InsertText == "Greet");
    }

    [Fact]
    public async Task GetCompletionsAsync_BelowTheIncludeLine_StillMapsOffsetsCorrectly()
    {
        const string source = "#include \"lib/util.cx\"\nnb.";

        var items = await Service.GetCompletionsAsync(source, source.Length, Token, context: Context);

        Assert.Contains(items, i => i.InsertText == "Player");
    }

    [Fact]
    public async Task GetHoverAsync_OnAnIncludedMethod_ShowsTheSignatureAndDocumentation()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.Greet(nb, \"x\");";

        var hover = await Service.GetHoverAsync(source, OffsetOf(source, "Greet"), Token, context: Context);

        Assert.NotNull(hover);
        Assert.Contains("Greet", hover.Signature);
        Assert.Contains("Says hello", hover.Documentation);
    }

    [Fact]
    public async Task GetSignatureHelpAsync_InsideAnIncludedCall_PointsAtTheSecondParameter()
    {
        const string source = "#include \"lib/util.cx\"\nUtil.Greet(nb, ";

        var help = await Service.GetSignatureHelpAsync(source, source.Length, Token, context: Context);

        var signature = Assert.Single(help!.Signatures);

        Assert.Equal(1, signature.ActiveParameter);
    }
}
