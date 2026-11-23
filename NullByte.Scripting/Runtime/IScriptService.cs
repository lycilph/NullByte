namespace NullByte.Scripting.Runtime;

public interface IScriptService
{
    Task<ScriptStartResult> Start(string hostname, string filename, IReadOnlyList<string> args, int threads = 1);
}