namespace NullByte.Core.Scripting;

public interface IScriptOperations
{
    IReadOnlyList<ScriptProcessInfo> ProcessesRunning(string hostname);

    int StartSimple(string hostname, string filename, IReadOnlyList<string> args, int threads = 1);

    bool Kill(int pid);
    int KillAll();

    bool ScriptRunning(string filename, string hostname);

    double GetScriptRam(string filename, string hostname);
}