using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Compilation;

/// <summary>
/// The sandbox rules: banned APIs (NB1007), blocking APIs (NB1008) and loops that never yield (NB1009).
/// They are exercised through the compiler, since the analyzer runs on a finished Roslyn compilation.
/// </summary>
public sealed class ScriptSecurityAnalyzerTests
{
    [Theory]
    [InlineData("var value = System.Environment.GetEnvironmentVariable(\"HOME\");")]
    [InlineData("var thread = new System.Threading.Thread(() => { });")]
    [InlineData("var name = typeof(string).Assembly.GetName();")]
    [InlineData("var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();")]
    [InlineData("var value = System.IO.File.ReadAllText(\"file.txt\");")]
    [InlineData("var task = Task.Run(() => { });")]
    [InlineData("var factory = Task.Factory;")]
    [InlineData("var timer = new System.Threading.Timer(_ => { }, null, 0, 1000);")]
    [InlineData("var type = Type.GetType(\"System.IO.File\");")]
    [InlineData("var method = typeof(string).GetMethod(\"Trim\", Type.EmptyTypes);")]
    [InlineData("var instance = Activator.CreateInstance(typeof(object));")]
    [InlineData("var reader = Delegate.CreateDelegate(typeof(Func<string, string>), Type.GetType(\"System.IO.File\")!.GetMethod(\"ReadAllText\", new[] { typeof(string) })!);")]
    [InlineData("GC.Collect();")]
    [InlineData("var type = typeof(System.Runtime.Loader.AssemblyLoadContext);")]
    [InlineData("var type = typeof(NullByte.Core.Domain.GameState);")]
    [InlineData("NullByte.Core.Services.ServerActions? actions = null;")]
    [InlineData("var concrete = (NullByte.Core.Scripting.NullByteApi)nb;")]
    public void BannedApi_IsRejected(string source)
    {
        CompileAssert.FailsWith(TestScripts.Compile(source), "NB1007");
    }

    [Fact]
    public void BannedApi_IsReportedOncePerUsage()
    {
        var result = TestScripts.Compile("var value = System.IO.File.ReadAllText(\"file.txt\");");

        Assert.False(result.Succeeded);
        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "NB1007");
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
    public void BlockingApi_IsRejected(string source)
    {
        CompileAssert.FailsWith(TestScripts.Compile(source), "NB1008");
    }

    [Theory]
    [InlineData("while (true) { nb.Print(\"loop\"); }")]
    [InlineData("for (;;) { nb.Print(\"loop\"); }")]
    [InlineData("do { nb.Print(\"loop\"); } while (true);")]
    [InlineData("for (var i = 0; true; i++) { nb.Print(i); }")]
    [InlineData("while (nb.HasRootAccess()) { nb.Print(\"loop\"); }")]
    [InlineData("var i = 0; while (i < 3) { i++; }")]
    // An await of something that is already complete never yields.
    [InlineData("while (true) { await Task.CompletedTask; }")]
    [InlineData("while (true) { await Task.FromResult(1); }")]
    // An await inside a local function does not make the loop around the call cooperative.
    [InlineData("async Task WaitAsync() => await Task.Delay(1);\nwhile (true) { WaitAsync(); }")]
    public void UnboundedLoopWithoutYield_IsRejected(string source)
    {
        CompileAssert.FailsWith(TestScripts.Compile(source), "NB1009");
    }

    [Theory]
    [InlineData("foreach (var value in new[] { 1, 2, 3 }) { nb.Print(value); }")]
    [InlineData("foreach (var host in nb.Scan()) { nb.Print(host); }")]
    [InlineData("for (var i = 0; i < 3; i++) { nb.Print(i); }")]
    [InlineData("while (true) { await nb.Sleep(TimeSpan.FromMilliseconds(1)); }")]
    [InlineData("async IAsyncEnumerable<int> Numbers() { yield return 1; await Task.Delay(1); }\nwhile (true) { await foreach (var value in Numbers()) nb.Print(value); }")]
    [InlineData("await Task.Delay(TimeSpan.FromMilliseconds(1)); await nb.Sleep(TimeSpan.FromSeconds(1));")]
    [InlineData("using System.IO; var value = 42; nb.Print(value);")]   // a banned namespace is fine until something in it is used
    [InlineData("var target = args[0];\nwhile (true)\n{\n    if (nb.GetServerSecurityLevel(target) > nb.GetServerMinSecurityLevel(target) + 5)\n        await nb.Weaken(target);\n    else if (nb.GetServerMoneyAvailable(target) < nb.GetServerMaxMoney(target) * 0.75)\n        await nb.Grow(target);\n    else\n        await nb.Hack(target);\n}")]
    public void SafeScript_IsAccepted(string source)
    {
        CompileAssert.Succeeds(TestScripts.Compile(source));
    }
}
