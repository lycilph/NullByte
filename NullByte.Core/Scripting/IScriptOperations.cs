namespace NullByte.Core.Scripting;

public interface IScriptOperations
{
    IReadOnlyList<ScriptProcessInfo> ProcessesRunning(string hostname);

    bool Kill(int pid);
    int KillAll();

    bool ScriptRunning(string filename, string hostname);

    double GetScriptRam(string filename, string hostname);
}