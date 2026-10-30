namespace NullByte.Scripting;

/// <summary>Internal: unwinds a script's await chain when stopped by the user.</summary>
internal sealed class ScriptStoppedException : Exception
{
    public ScriptStoppedException() : base("Script stopped by user.") { }
}