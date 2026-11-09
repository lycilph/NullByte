namespace NullByte.Scripting.Compilation;

/// <summary>
/// Contains the emitted assembly for a successfully compiled script.
/// </summary>
public sealed record CompiledScript(
    byte[] Assembly,
    byte[] Symbols);