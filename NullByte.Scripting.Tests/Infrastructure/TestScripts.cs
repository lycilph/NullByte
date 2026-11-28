using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Infrastructure;

/// <summary>Compiles player scripts for the tests that need to look at the result.</summary>
internal static class TestScripts
{
    /// <summary>Compiles a script. Pass <paramref name="files"/> to give it something to #include.</summary>
    public static ScriptCompilationResult Compile(
        string source,
        string fileName = "test.cx",
        Dictionary<string, string>? files = null)
        => new ScriptCompiler().Compile(source, fileName, files is null ? null : new FakeSources(files));

    /// <summary>Compiles a script that is expected to compile cleanly.</summary>
    public static CompiledScript CompileOrFail(string source, string fileName = "test.cx")
        => CompileAssert.Succeeds(Compile(source, fileName));
}
