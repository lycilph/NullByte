using System.Runtime.CompilerServices;
using NullByte.Core.Scripting;
using NullByte.Core.Scripting.Activities;
using NullByte.Core.Services;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests.Loading;

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
            "unload-test.cx");

        AssertCompilationSucceeded(result);

        var script = Assert.IsType<CompiledScript>(result.Script);
        var weakReference = await RunAndUnloadAsync(script);

        WaitForCollection(weakReference);

        Assert.False(weakReference.IsAlive, "The script AssemblyLoadContext was not collected.");
    }

    [Fact]
    public async Task RecompiledScript_AllowsPreviousLoadContextToBeCollected()
    {
        var compiler = new ScriptCompiler();

        var firstResult = compiler.Compile(
            """
            nb.Print("Version 1");
            """,
            "hack.cx");

        AssertCompilationSucceeded(firstResult);

        var firstScript = Assert.IsType<CompiledScript>(firstResult.Script);
        var firstContext = await RunAndUnloadAsync(firstScript);

        var secondResult = compiler.Compile(
            """
            nb.Print("Version 2");
            """,
            "hack.cx");

        AssertCompilationSucceeded(secondResult);

        var secondScript = Assert.IsType<CompiledScript>(secondResult.Script);

        await RunWithoutTrackingAsync(secondScript);

        WaitForCollection(firstContext);

        Assert.False(firstContext.IsAlive, "The previous script AssemblyLoadContext was not collected.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> RunAndUnloadAsync(CompiledScript script)
    {
        WeakReference? weakReference = null;

        var runner = new ScriptRunner(new ScriptAssemblyLoader())
        {
            LoadContextCreated = context =>
                weakReference = new WeakReference(context, trackResurrection: false)
        };

        await runner.RunAsync(script, new TestNullByteApi(), []);

        runner.LoadContextCreated = null;
        runner = null!;

        return Assert.IsType<WeakReference>(weakReference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task RunWithoutTrackingAsync(CompiledScript script)
    {
        var runner = new ScriptRunner(new ScriptAssemblyLoader());

        await runner.RunAsync(script, new TestNullByteApi(), []);

        runner = null!;
    }

    private static void WaitForCollection(WeakReference weakReference)
    {
        for (var attempt = 0; attempt < 10 && weakReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Thread.Sleep(10);
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
        public INullByteFilesApi Files => throw new NotSupportedException();
        public INullByteBotnetApi Botnet => throw new NotSupportedException();
        public INullByteActivitiesApi Activities => throw new NotSupportedException();

        public void Print(object? value) { }
        public Task Sleep(TimeSpan duration) => Task.CompletedTask;
        public Task<HackResult> Hack(string hostname) => throw new NotSupportedException();
        public Task<GrowResult> Grow(string hostname) => throw new NotSupportedException();
        public Task<WeakenResult> Weaken(string hostname) => throw new NotSupportedException();
        public double GetServerMoneyAvailable(string hostname) => throw new NotSupportedException();
        public double GetServerMaxMoney(string hostname) => throw new NotSupportedException();
        public double GetServerSecurityLevel(string hostname) => throw new NotSupportedException();
        public double GetServerMinSecurityLevel(string hostname) => throw new NotSupportedException();
        public double GetHackingLevel() => throw new NotSupportedException();
        public int GetServerRequiredHackingLevel(string hostname) => throw new NotSupportedException();
        public double GetServerMaxRam(string hostname) => throw new NotSupportedException();
        public double GetServerUsedRam(string hostname) => throw new NotSupportedException();
        public bool HasRootAccess(string hostname) => throw new NotSupportedException();
        public NukeResult Nuke(string hostname) => throw new NotSupportedException();
        public IReadOnlyList<string> Scan(string hostname) => throw new NotSupportedException();

        public IReadOnlyList<ScriptProcessInfo> Ps(string hostname = "")
        {
            throw new NotImplementedException();
        }

        public Task<int> RunAsync(string filename, params string[] args)
        {
            throw new NotImplementedException();
        }

        public Task<int> ExecAsync(string filename, string hostname, params string[] args)
        {
            throw new NotImplementedException();
        }

        public bool Kill(int pid)
        {
            throw new NotImplementedException();
        }

        public int KillAll()
        {
            throw new NotImplementedException();
        }

        public bool ScriptRunning(string filename, string hostname = "")
        {
            throw new NotImplementedException();
        }

        public double GetScriptRam(string filename, string hostname = "")
        {
            throw new NotImplementedException();
        }
    }
}