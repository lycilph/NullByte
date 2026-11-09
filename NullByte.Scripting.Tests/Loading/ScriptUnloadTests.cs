using System.Runtime.CompilerServices;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;
using NullByte.Scripting.Tests.Runtime;

namespace NullByte.Scripting.Tests.Loading;

[Collection(ScriptRuntimeCollection.Name)]
public sealed class ScriptUnloadTests
{
    [Fact]
    public async Task CompletedScript_AllowsLoadContextToBeCollected()
    {
        var compiler = new ScriptCompiler();

        var result = compiler.Compile(
            """
            nb.Print("Hello");
            """,
            "unload-test.cs");

        AssertCompilationSucceeded(result);

        var script = Assert.IsType<CompiledScript>(result.Script);
        var weakReference = await LoadRunAndUnloadAsync(script);

        ForceGarbageCollection();

        Assert.False(weakReference.IsAlive, "The script AssemblyLoadContext was not collected.");
    }

    [Fact]
    public async Task RecompiledScript_AllowsOldLoadContextToBeCollected()
    {
        var compiler = new ScriptCompiler();

        var firstResult = compiler.Compile(
            """
            nb.Print("Version 1");
            """,
            "recompile-test.cs");

        AssertCompilationSucceeded(firstResult);

        var firstScript = Assert.IsType<CompiledScript>(firstResult.Script);

        var weakReference = await LoadRunAndUnloadAsync(firstScript);

        var secondResult = compiler.Compile(
            """
            nb.Print("Version 2");
            """,
            "recompile-test.cs");

        AssertCompilationSucceeded(secondResult);

        var secondScript = Assert.IsType<CompiledScript>(secondResult.Script);

        await RunAsync(secondScript);

        ForceGarbageCollection();

        Assert.False(weakReference.IsAlive, "The previous script AssemblyLoadContext was not collected.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> LoadRunAndUnloadAsync(CompiledScript script)
    {
        var loader = new ScriptAssemblyLoader();
        var runner = new ScriptRunner(loader);
        var api = new TestNullByteApi();

        var weakReference = await RunAndCaptureContextAsync(
            runner,
            loader,
            script,
            api);

        runner = null!;
        loader = null!;
        api = null!;

        return weakReference;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> RunAndCaptureContextAsync(
        ScriptRunner runner,
        ScriptAssemblyLoader loader,
        CompiledScript script,
        INullByteApi api)
    {
        var loaded = loader.Load(script);

        var weakReference = new WeakReference(
            loaded.LoadContext,
            trackResurrection: false);

        loader.Unload(loaded);

        loaded = null!;

        await runner.RunAsync(script, api, []);

        return weakReference;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task RunAsync(CompiledScript script)
    {
        var runner = new ScriptRunner(new ScriptAssemblyLoader());

        await runner.RunAsync(script, new TestNullByteApi(), []);
    }

    private static void ForceGarbageCollection()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
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

    private sealed class TestNullByteApi : INullByteApi
    {
        public void Print(object? value) {}
        public Task Sleep(TimeSpan duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<HackResult> Hack(string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GrowResult> Grow(string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WeakenResult> Weaken(string hostname, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public double GetServerMoneyAvailable(string hostname) => throw new NotSupportedException();
        public double GetServerMaxMoney(string hostname) => throw new NotSupportedException();
        public double GetServerSecurityLevel(string hostname) => throw new NotSupportedException();
        public double GetServerMinSecurityLevel(string hostname) => throw new NotSupportedException();
    }
}