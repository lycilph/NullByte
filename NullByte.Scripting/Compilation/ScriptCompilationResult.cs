namespace NullByte.Scripting.Compilation;

/// <summary>
/// Represents the result of compiling player-written source code.
/// </summary>
public sealed class ScriptCompilationResult
{
    private ScriptCompilationResult(CompiledScript? script, IReadOnlyList<ScriptDiagnostic> diagnostics)
    {
        Script = script;
        Diagnostics = diagnostics;
    }

    public bool Succeeded => Script is not null;

    public CompiledScript? Script { get; }

    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }

    public static ScriptCompilationResult Success(CompiledScript script, IReadOnlyList<ScriptDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new ScriptCompilationResult(script, diagnostics.ToArray());
    }

    public static ScriptCompilationResult Failure(IReadOnlyList<ScriptDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new ScriptCompilationResult(script: null,diagnostics.ToArray());
    }
}