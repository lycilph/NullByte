namespace NullByte.Editor;

public sealed record ExternalEditorStatus(IReadOnlyList<ExternalEditorConnectionInfo> Connections);