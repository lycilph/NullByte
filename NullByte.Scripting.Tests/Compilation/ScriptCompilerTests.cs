using System.Reflection;
using System.Runtime.Loader;
using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Compilation;

public sealed class ScriptCompilerTests
{
    private readonly ScriptCompiler compiler = new();

    [Fact]
    public void Compile_WithSimpleTopLevelScript_Succeeds()
    {
        const string source = """
            var answer = 42;
            nb.Print(answer);
            """;

        var result = compiler.Compile(source, "simple.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error);
    }

    [Fact]
    public void Compile_WithRoslynGeneratedArgs_Succeeds()
    {
        const string source = """
            var target = args[0];
            nb.Print(target);
            """;

        var result = compiler.Compile(source, "args.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
    }

    [Fact]
    public void Compile_WithInjectedNullByteApi_Succeeds()
    {
        const string source = """
            var target = args[0];

            nb.Print($"Target: {target}");

            var money = nb.GetServerMoneyAvailable(target);
            var maximumMoney = nb.GetServerMaxMoney(target);
            var security = nb.GetServerSecurityLevel(target);
            var minimumSecurity = nb.GetServerMinSecurityLevel(target);

            nb.Print($"{money}/{maximumMoney}, {security}/{minimumSecurity}");
            """;

        var result = compiler.Compile(source, "api.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
    }

    [Fact]
    public void Compile_ProvidesDefaultSystemUsing()
    {
        const string source = """
        TimeSpan delay = TimeSpan.FromSeconds(1);
        nb.Print(delay);
        """;

        var result = compiler.Compile(source, "default-system-using.cs");

        AssertCompilationSucceeded(result);
    }

    [Fact]
    public void Compile_WithTopLevelAwait_Succeeds()
    {
        const string source = """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            nb.Print("Finished");
            """;

        var result = compiler.Compile(source, "await.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
    }

    [Fact]
    public void Compile_WithServerActions_Succeeds()
    {
        const string source = """
            var target = args[0];

            await nb.Hack(target);
            await nb.Grow(target);
            await nb.Weaken(target);
            """;

        var result = compiler.Compile(source, "actions.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
    }

    [Fact]
    public void Compile_WithAutomationScript_Succeeds()
    {
        const string source = """
            var target = args[0];

            while (true)
            {
                if (nb.GetServerSecurityLevel(target) > nb.GetServerMinSecurityLevel(target) + 5)
                    await nb.Weaken(target);
                else if (nb.GetServerMoneyAvailable(target) < nb.GetServerMaxMoney(target) * 0.75)
                    await nb.Grow(target);
                else
                    await nb.Hack(target);
            }
            """;

        var result = compiler.Compile(source, "early-hack.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
    }

    [Fact]
    public void Compile_WithUsingDirective_Succeeds()
    {
        const string source = """
            using System.Globalization;

            var value = 12.5;

            nb.Print(value.ToString(CultureInfo.InvariantCulture));
            """;

        var result = compiler.Compile(source, "using.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
    }

    [Fact]
    public void Compile_WithSyntaxError_Fails()
    {
        const string source = """
            var target = args[0]
            nb.Print(target);
            """;

        var result = compiler.Compile(source, "syntax-error.cs");

        Assert.False(result.Succeeded);
        Assert.Null(result.Script);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error);
    }

    [Fact]
    public void Compile_WithSemanticError_Fails()
    {
        const string source = """
            var target = args[0];
            nb.Print(missingValue);
            """;

        var result = compiler.Compile(source, "semantic-error.cs");

        Assert.False(result.Succeeded);
        Assert.Null(result.Script);

        var diagnostic = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Id == "CS0103");

        Assert.Equal(ScriptDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("missingValue", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_WithSemanticError_ReportsPlayerLineAndColumn()
    {
        const string source = """
            var target = args[0];
            nb.Print(target);
            nb.Print(missingValue);
            """;

        var result = compiler.Compile(source, "line-mapping.cs");

        var diagnostic = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Id == "CS0103");

        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(10, diagnostic.Column);
    }

    [Fact]
    public void Compile_WithUsingDirective_ReportsPlayerLine()
    {
        const string source = """
            using System.Globalization;

            var target = args[0];
            nb.Print(missingValue);
            """;

        var result = compiler.Compile(source, "using-mapping.cs");

        var diagnostic = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Id == "CS0103");

        Assert.Equal(4, diagnostic.Line);
        Assert.Equal(10, diagnostic.Column);
    }

    [Fact]
    public void Compile_WithSyntaxError_ReportsPlayerLine()
    {
        const string source = """
            var target = args[0];

            if (target ==)
            {
                nb.Print(target);
            }
            """;

        var result = compiler.Compile(source, "syntax-location.cs");

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Severity == ScriptDiagnosticSeverity.Error &&
                diagnostic.Line == 3);
    }

    [Fact]
    public void Compile_WithWarning_SucceedsAndReturnsWarning()
    {
        const string source = """
            var unused = 42;
            nb.Print("Running");
            """;

        var result = compiler.Compile(source, "warning.cs");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Severity == ScriptDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Compile_ProducesAssemblyAndPortableSymbols()
    {
        const string source = """
            nb.Print("Hello");
            """;

        var result = compiler.Compile(source, "output.cs");

        AssertCompilationSucceeded(result);

        var script = Assert.IsType<CompiledScript>(result.Script);

        Assert.NotEmpty(script.Assembly);
        Assert.NotEmpty(script.Symbols);
    }

    [Fact]
    public void Compile_CalledTwice_ProducesDistinctAssemblies()
    {
        const string source = """
            nb.Print("Hello");
            """;

        var firstResult = compiler.Compile(source, "first.cs");
        var secondResult = compiler.Compile(source, "second.cs");

        var firstScript = Assert.IsType<CompiledScript>(firstResult.Script);
        var secondScript = Assert.IsType<CompiledScript>(secondResult.Script);

        var firstContext = new TestLoadContext();
        var secondContext = new TestLoadContext();

        try
        {
            var firstAssembly = LoadAssembly(firstContext, firstScript);
            var secondAssembly = LoadAssembly(secondContext, secondScript);

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
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(null!, "null.cs"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Compile_WithInvalidFileName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => compiler.Compile("nb.Print(\"Hello\");",fileName));
    }

    private static void AssertCompilationSucceeded(ScriptCompilationResult result)
    {
        var diagnostics = string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(
                diagnostic =>
                    $"{diagnostic.Id} " +
                    $"({diagnostic.Line},{diagnostic.Column}): " +
                    diagnostic.Message));

        Assert.True(result.Succeeded, diagnostics);
    }

    private static Assembly LoadAssembly(AssemblyLoadContext loadContext, CompiledScript script)
    {
        using var assemblyStream = new MemoryStream(script.Assembly, writable: false);
        using var symbolsStream = new MemoryStream(script.Symbols, writable: false);

        return loadContext.LoadFromStream(assemblyStream, symbolsStream);
    }

    private sealed class TestLoadContext : AssemblyLoadContext
    {
        public TestLoadContext() : base(isCollectible: true) {}

        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}