using NullByte.Core.Persistence;

namespace NullByte.Scripting.Runtime;

public sealed record ScriptRestoreResult(ProcessSave Saved, ScriptStartResult Start);