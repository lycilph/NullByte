using NullByte.Core.Domain;
using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Runtime;

/// <summary>Reads includes from one server's files. Game thread only.</summary>
public sealed class ServerSourceProvider(VirtualFileSystem files, string hostname) : IScriptSourceProvider
{
    public bool TryReadSource(string path, out string? source) => files.TryReadFile(hostname, path, out source);
}