namespace NullByte.Core.Scripting;

public interface IScriptOperations
{
    IReadOnlyList<ScriptProcessInfo> Ps(string hostname);

    bool Kill(int pid);

    bool ScriptRunning(string filename, string hostname);

    double GetScriptRam(string filename, string hostname);
}