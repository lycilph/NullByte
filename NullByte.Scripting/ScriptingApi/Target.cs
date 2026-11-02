namespace NullByte.Scripting.ScriptingApi;

public sealed record Target
(
    string Id,
    string Name,
    int MinLevel,
    int RequiredCpu = 0,
    int RequiredRam = 0
);
