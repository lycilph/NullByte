namespace NullByte.ConsoleUI.Scripting;

public sealed class ScriptGlobals
{
    public required IScriptApi Api { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}