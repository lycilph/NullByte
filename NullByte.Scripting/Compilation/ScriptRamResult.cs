using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Runtime;

public sealed record ScriptRamResult(
    bool Succeeded,
    double Ram,
    string FailureMessage,
    IReadOnlyList<ScriptDiagnostic> Diagnostics)
{
    public static ScriptRamResult Success(double ram) => new(true, ram, string.Empty, []);

    public static ScriptRamResult Failure(string message, IReadOnlyList<ScriptDiagnostic>? diagnostics = null)
        => new(false, 0.0, message, diagnostics ?? []);
}