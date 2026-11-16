using NullByte.Core.Domain;

namespace NullByte.Core.Scripting;

public class NullByteFilesApi(VirtualFileSystem fileSystem, string processHostname) : INullByteFilesApi
{
    public IReadOnlyList<string> List(string hostname = "") => fileSystem.ListFiles(GetTargetHostname(hostname));

    private string GetTargetHostname(string hostname) => string.IsNullOrWhiteSpace(hostname) ? processHostname : hostname;
}
