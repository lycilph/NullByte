using NullByte.Core.Domain;
using NullByte.Core.Simulation;
using NullByte.Scripting.LanguageService;

namespace NullByte.Editor;

/// What the editor server needs from the running game. Re-evaluated per request, so `load` just works.
public sealed record EditorBackend(GameState State, IGameThread GameThread);

internal sealed record StatResult(string Type, long Size, long Created, long Modified);
internal sealed record DirectoryEntry(string Name, string Type);
internal sealed record DirectoryResult(IReadOnlyList<DirectoryEntry> Entries);
internal sealed record FileResult(string Content);
internal sealed record WriteResult(bool Created);

internal sealed record DiagnosticInfo(string Id, string Message, string Severity, int Line, int Column, string? File = null);
internal sealed record DiagnoseResult(IReadOnlyList<DiagnosticInfo> Diagnostics);
internal sealed record CompleteResult(IReadOnlyList<ScriptCompletion> Items);
internal sealed record HoverResult(ScriptHover? Hover);
internal sealed record SignatureHelpResult(ScriptSignatureHelp? Help);

internal sealed class EditorException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}