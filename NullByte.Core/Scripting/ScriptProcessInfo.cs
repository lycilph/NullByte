namespace NullByte.Core.Scripting;

public sealed record ScriptProcessInfo(
    int Pid,
    string Filename,
    IReadOnlyList<string> Args,
    int Threads,
    string Hostname);