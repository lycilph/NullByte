namespace NullByte.Scripting;

// ---------------------------------------------------------------------------
// ScriptInstance: a compiled script and its live state. The instance object
// is the script's "body" — output, error, data. Execution is a running Task
// suspended on awaits; it is never serialized (source restart model).
// ---------------------------------------------------------------------------

public sealed class ScriptInstance
{

}