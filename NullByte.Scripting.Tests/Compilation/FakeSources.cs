using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Compilation;

internal sealed class FakeSources(Dictionary<string, string> files) : IScriptSourceProvider
{
    public bool TryReadSource(string path, out string? source) => files.TryGetValue(path, out source);
}
