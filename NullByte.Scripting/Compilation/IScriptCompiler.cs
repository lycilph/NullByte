namespace NullByte.Scripting.Compilation;

/// <summary>
/// Compiles player-written C# source code into an executable script assembly.
/// </summary>
public interface IScriptCompiler
{
    ScriptCompilationResult Compile(string source, string fileName);
}