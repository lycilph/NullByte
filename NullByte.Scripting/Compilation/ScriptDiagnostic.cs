namespace NullByte.Scripting.Compilation;

/// <summary>
/// Describes a compiler diagnostic in player-written source code.
/// </summary>
public sealed record ScriptDiagnostic(
    string Id,
    string Message,
    ScriptDiagnosticSeverity Severity,
    int Line,
    int Column);