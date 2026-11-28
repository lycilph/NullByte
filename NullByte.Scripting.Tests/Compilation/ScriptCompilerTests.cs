using System.Reflection;
using System.Runtime.Loader;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Compilation;

/// <summary>
/// The compile pipeline as a whole: what compiles, what the result contains, and how bad input is
/// reported. The analyzer rules, diagnostic positions, RAM costs and includes have their own test classes.
/// </summary>
public sealed class ScriptCompilerTests
{
    public static TheoryData<string> ValidScripts => new()
    {
        "var answer = 42;\nnb.Print(answer);",
        "var target = args[0];\nnb.Print(target);",
        """
        var target = args[0];

        nb.Print($"Target: {target}");

        var money = nb.GetServerMoneyAvailable(target);
        var maximumMoney = nb.GetServerMaxMoney(target);
        var security = nb.GetServerSecurityLevel(target);
        var minimumSecurity = nb.GetServerMinSecurityLevel(target);

        nb.Print($"{money}/{maximumMoney}, {security}/{minimumSecurity}");
        """,
        """
        var money = nb.Player.Money;
        var hackingExperience = nb.Player.Skills.Hacking.Experience;
        var hackingLevel = nb.Player.Skills.Hacking.Level;
        var charismaLevel = nb.Player.Skills.Charisma.Level;
        nb.Print($"{money}/{hackingExperience}/{hackingLevel}/{charismaLevel}");
        """,
        "TimeSpan delay = TimeSpan.FromSeconds(1);\nnb.Print(delay);",
        "await nb.Sleep(TimeSpan.FromSeconds(1));\nnb.Print(\"Finished\");",
        "await Task.Delay(TimeSpan.FromMilliseconds(1));",
        "var target = args[0];\nawait nb.Hack(target);\nawait nb.Grow(target);\nawait nb.Weaken(target);",
        "using System.Globalization;\n\nvar value = 12.5;\n\nnb.Print(value.ToString(CultureInfo.InvariantCulture));"
    };

    [Theory]
    [MemberData(nameof(ValidScripts))]
    public void Compile_WithValidScript_Succeeds(string source)
    {
        var result = TestScripts.Compile(source);

        CompileAssert.Succeeds(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("nb.GetHackingLevel();", "CS1061")]                                       // an API member that was removed
    [InlineData("var process = new System.Diagnostics.Process();", "CS0234")]             // not part of the script references
    [InlineData("var client = new System.Net.Http.HttpClient();", "CS0234")]
    public void Compile_WithUnavailableApi_ReportsCompilerError(string source, string diagnosticId)
    {
        CompileAssert.FailsWith(TestScripts.Compile(source), diagnosticId);
    }

    [Fact]
    public void Compile_WithSyntaxError_Fails()
    {
        var result = TestScripts.Compile("var target = args[0]\nnb.Print(target);");

        Assert.False(result.Succeeded);
        Assert.Null(result.Script);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error);
    }

    [Fact]
    public void Compile_WithSemanticError_ReportsTheErrorAndTheUnknownName()
    {
        var result = TestScripts.Compile("var target = args[0];\nnb.Print(missingValue);");

        var diagnostic = CompileAssert.FailsWith(result, "CS0103");

        Assert.Equal(ScriptDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("missingValue", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_WithWarning_SucceedsAndReturnsTheWarning()
    {
        var result = TestScripts.Compile("var unused = 42;\nnb.Print(\"Running\");");

        CompileAssert.Succeeds(result);

        var warning = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Warning);

        Assert.Equal(warning.Line, warning.EndLine);
        Assert.True(warning.EndColumn > warning.Column);
    }

    [Fact]
    public void Compile_WithValidScript_ProducesAssemblyAndPortableSymbols()
    {
        var script = CompileAssert.Succeeds(TestScripts.Compile("nb.Print(\"Hello\");"));

        Assert.NotEmpty(script.Assembly);
        Assert.NotEmpty(script.Symbols);
    }

    [Fact]
    public void Compile_CalledTwice_ProducesAssembliesWithDistinctNames()
    {
        var first = TestScripts.CompileOrFail("nb.Print(\"Hello\");", "first.cx");
        var second = TestScripts.CompileOrFail("nb.Print(\"Hello\");", "second.cx");

        var firstContext = new TestLoadContext();
        var secondContext = new TestLoadContext();

        try
        {
            var firstAssembly = LoadAssembly(firstContext, first);
            var secondAssembly = LoadAssembly(secondContext, second);

            Assert.NotEqual(firstAssembly.GetName().Name, secondAssembly.GetName().Name);
        }
        finally
        {
            firstContext.Unload();
            secondContext.Unload();
        }
    }

    [Fact]
    public void Compile_WithNullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TestScripts.Compile(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Compile_WithInvalidFileName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => TestScripts.Compile("nb.Print(\"Hello\");", fileName));
    }

    private static Assembly LoadAssembly(AssemblyLoadContext loadContext, CompiledScript script)
    {
        using var assemblyStream = new MemoryStream(script.Assembly, writable: false);
        using var symbolsStream = new MemoryStream(script.Symbols, writable: false);

        return loadContext.LoadFromStream(assemblyStream, symbolsStream);
    }

    private sealed class TestLoadContext : AssemblyLoadContext
    {
        public TestLoadContext() : base(isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
