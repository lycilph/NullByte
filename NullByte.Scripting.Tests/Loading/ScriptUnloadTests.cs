using System.Runtime.CompilerServices;
using NSubstitute;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Loading;

/// <summary>
/// A script's load context must be collectible once the script is done, or every run would leak an assembly.
/// These tests depend on the garbage collector, so they are the one place where waiting in a loop is expected.
/// </summary>
public sealed class ScriptUnloadTests
{
    [Fact]
    public async Task CompletedScript_AllowsTheLoadContextToBeCollected()
    {
        var script = TestScripts.CompileOrFail("nb.Print(\"Hello\");", "unload-test.cx");

        var weakReference = await RunAndUnloadAsync(script);

        WaitForCollection(weakReference);

        Assert.False(weakReference.IsAlive, "The script AssemblyLoadContext was not collected.");
    }

    [Fact]
    public async Task RecompiledScript_AllowsThePreviousLoadContextToBeCollected()
    {
        var first = TestScripts.CompileOrFail("nb.Print(\"Version 1\");", "hack.cx");
        var firstContext = await RunAndUnloadAsync(first);

        var second = TestScripts.CompileOrFail("nb.Print(\"Version 2\");", "hack.cx");
        await RunWithoutTrackingAsync(second);

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

        await runner.RunAsync(script, Substitute.For<INullByteApi>(), []);

        runner.LoadContextCreated = null;
        runner = null!;

        return Assert.IsType<WeakReference>(weakReference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task RunWithoutTrackingAsync(CompiledScript script)
    {
        var runner = new ScriptRunner(new ScriptAssemblyLoader());

        await runner.RunAsync(script, Substitute.For<INullByteApi>(), []);

        runner = null!;
    }

    private static void WaitForCollection(WeakReference reference)
    {
        for (var attempt = 0; attempt < 10 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Thread.Sleep(10);
        }
    }
}
