using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Infrastructure;

/// <summary>An in-memory set of files that a script can #include.</summary>
internal sealed class FakeSources(Dictionary<string, string> files) : IScriptSourceProvider
{
    public bool TryReadSource(string path, out string? source) => files.TryGetValue(path, out source);
}
