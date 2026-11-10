using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Runtime;

/// <summary>
/// Describes the result of attempting to start a player script.
/// </summary>
public sealed record ScriptStartResult(bool Succeeded, string FailureMessage, IReadOnlyList<ScriptDiagnostic> Diagnostics)
{
    public static ScriptStartResult Success(IReadOnlyList<ScriptDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new ScriptStartResult(Succeeded: true, FailureMessage: string.Empty, Diagnostics: diagnostics.ToArray());
    }

    public static ScriptStartResult Failure(string message, IReadOnlyList<ScriptDiagnostic>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ScriptStartResult(Succeeded: false, FailureMessage: message, Diagnostics: diagnostics?.ToArray() ?? []);
    }
}