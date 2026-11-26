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

        var result = compiler.Compile(source, "simple.cx");

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

        var result = compiler.Compile(source, "args.cx");

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

        var result = compiler.Compile(source, "api.cx");

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

        var result = compiler.Compile(source, "default-system-using.cx");

        AssertCompilationSucceeded(result);
    }

    [Fact]
    public void Compile_WithTopLevelAwait_Succeeds()
    {
        const string source = """
            await nb.Sleep(TimeSpan.FromSeconds(1));
            nb.Print("Finished");
            """;

        var result = compiler.Compile(source, "await.cx");

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

        var result = compiler.Compile(source, "actions.cx");

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

        var result = compiler.Compile(source, "early-hack.cx");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
    }

    [Theory]
    [InlineData("var value = System.Environment.GetEnvironmentVariable(\"HOME\");")]
    [InlineData("var thread = new System.Threading.Thread(() => { });")]
    [InlineData("var name = typeof(string).Assembly.GetName();")]
    [InlineData("var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();")]
    [InlineData("var value = System.IO.File.ReadAllText(\"file.txt\");")]
    public void Compile_WithBannedApiUsage_Fails(string source)
    {
        var result = compiler.Compile(source, "security.cx");

        Assert.False(result.Succeeded);
        Assert.Null(result.Script);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "NB1007");
    }

    [Theory]
    [InlineData("var process = new System.Diagnostics.Process();", "CS0234")]
    [InlineData("var client = new System.Net.Http.HttpClient();", "CS0234")]
    public void Compile_WithUnavailableHostApiReference_Fails(string source, string diagnosticId)
    {
        var result = compiler.Compile(source, "unavailable-host-api.cx");

        Assert.False(result.Succeeded);
        Assert.Null(result.Script);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    [Theory]
    [InlineData("Task.Delay(TimeSpan.FromMilliseconds(1)).Wait();")]
    [InlineData("Task.WaitAll(Task.Delay(1));")]
    [InlineData("var task = Task.FromResult(42); var value = task.Result;")]
    [InlineData("var valueTask = new ValueTask<int>(42); var value = valueTask.Result;")]
    [InlineData("System.Threading.Monitor.Wait(new object());")]
    [InlineData("new System.Threading.SemaphoreSlim(0).Wait();")]
    [InlineData("new System.Threading.ManualResetEventSlim().Wait();")]
    [InlineData("new System.Threading.CountdownEvent(1).Wait();")]
    [InlineData("new System.Threading.Barrier(1).SignalAndWait();")]
    [InlineData("System.Threading.SpinWait.SpinUntil(() => true);")]
    [InlineData("Task.Delay(1).GetAwaiter().GetResult();")]
    public void Compile_WithSynchronousBlockingApiUsage_Fails(string source)
    {
        var result = compiler.Compile(source, "blocking-api.cx");

        Assert.False(result.Succeeded);
        Assert.Null(result.Script);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "NB1008");
    }

    [Fact]
    public void Compile_WithAsynchronousWaiting_Succeeds()
    {
        const string source = "await Task.Delay(TimeSpan.FromMilliseconds(1)); await nb.Sleep(TimeSpan.FromSeconds(1));";

        var result = compiler.Compile(source, "async-waiting.cx");

        AssertCompilationSucceeded(result);
    }

    [Fact]
    public void Compile_WithBannedNamespaceUsingButNoApiUsage_Succeeds()
    {
        const string source = "using System.IO; var value = 42; nb.Print(value);";

        var result = compiler.Compile(source, "unused-namespace.cx");

        AssertCompilationSucceeded(result);
    }

    [Fact]
    public void Compile_WithTaskAwait_Succeeds()
    {
        const string source = "await Task.Delay(TimeSpan.FromMilliseconds(1));";

        var result = compiler.Compile(source, "task.cx");

        AssertCompilationSucceeded(result);
    }

    [Fact]
    public void Compile_WithUsingDirective_Succeeds()
    {
        const string source = """
            using System.Globalization;

            var value = 12.5;

            nb.Print(value.ToString(CultureInfo.InvariantCulture));
            """;

        var result = compiler.Compile(source, "using.cx");

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

        var result = compiler.Compile(source, "syntax-error.cx");

        Assert.False(result.Succeeded);
        Assert.Null(result.Script);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error);
    }

    [Fact]
    public void Compile_WithMissingToken_ReportsEmptyRange()
    {
        const string source = "var target = args[0]";

        var result = compiler.Compile(source, "missing-token.cx");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "CS1002");

        Assert.Equal(diagnostic.Line, diagnostic.EndLine);
        Assert.Equal(diagnostic.Column, diagnostic.EndColumn);
    }

    [Fact]
    public void Compile_WithSemanticError_Fails()
    {
        const string source = """
            var target = args[0];
            nb.Print(missingValue);
            """;

        var result = compiler.Compile(source, "semantic-error.cx");

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

        var result = compiler.Compile(source, "line-mapping.cx");

        var diagnostic = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Id == "CS0103");

        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(10, diagnostic.Column);
        Assert.Equal(3, diagnostic.EndLine);
        Assert.Equal(22, diagnostic.EndColumn);
    }

    [Fact]
    public void Compile_WithUsingDirective_ReportsPlayerLine()
    {
        const string source = """
            using System.Globalization;

            var target = args[0];
            nb.Print(missingValue);
            """;

        var result = compiler.Compile(source, "using-mapping.cx");

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

        var result = compiler.Compile(source, "syntax-location.cx");

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

        var result = compiler.Compile(source, "warning.cx");

        AssertCompilationSucceeded(result);
        Assert.NotNull(result.Script);
        var warning = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Warning);

        Assert.Equal(warning.Line, warning.EndLine);
        Assert.True(warning.EndColumn > warning.Column);
    }

    [Fact]
    public void Compile_ProducesAssemblyAndPortableSymbols()
    {
        const string source = """
            nb.Print("Hello");
            """;

        var result = compiler.Compile(source, "output.cx");

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

        var firstResult = compiler.Compile(source, "first.cx");
        var secondResult = compiler.Compile(source, "second.cx");

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
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(null!, "null.cx"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Compile_WithInvalidFileName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => compiler.Compile("nb.Print(\"Hello\");", fileName));
    }

    [Fact]
    public void Compile_ReportsBaseRamCostForScriptWithoutApiCalls()
    {
        const string source = """
        var value = 42;
        """;

        var result = compiler.Compile(source, "ram.cx");

        AssertCompilationSucceeded(result);

        var script = Assert.IsType<CompiledScript>(result.Script);

        Assert.Equal(1.6, script.RamCost);
    }

    [Fact]
    public void Compile_AddsRamCostForApiMethod()
    {
        const string source = """
        nb.Print("Hello");
        """;

        var result = compiler.Compile(source, "ram.cx");

        AssertCompilationSucceeded(result);

        var script = Assert.IsType<CompiledScript>(result.Script);

        Assert.Equal(1.7, script.RamCost, 4);
    }

    [Fact]
    public void Compile_CountsEachApiMethodOnlyOnce()
    {
        const string source = """
        nb.Print("One");
        nb.Print("Two");
        nb.Print("Three");
        """;

        var result = compiler.Compile(source, "ram.cx");

        AssertCompilationSucceeded(result);

        var script = Assert.IsType<CompiledScript>(result.Script);

        Assert.Equal(1.7, script.RamCost, 4);
    }

    [Fact]
    public void Compile_AddsDistinctApiMethods()
    {
        const string source = """
        nb.Print("Starting");
        await nb.Hack("n00dles");
        await nb.Hack("n00dles");
        await nb.Weaken("n00dles");
        """;

        var result = compiler.Compile(source, "ram.cx");

        AssertCompilationSucceeded(result);

        var script = Assert.IsType<CompiledScript>(result.Script);

        Assert.Equal(5.1, script.RamCost);
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
        public TestLoadContext() : base(isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}