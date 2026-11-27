using NullByte.Core.Domain;

namespace NullByte.Core.Scripting;

public class NullByteFilesApi : INullByteFilesApi
{
    private readonly VirtualFileSystem fileSystem;
    private readonly Network network;
    private readonly ServerId processHostServerId;
    private readonly NarrativeState narrative;

    public NullByteFilesApi(VirtualFileSystem fileSystem, Network network, ServerId processHostServerId, NarrativeState narrative)
    {
        this.fileSystem = fileSystem;
        this.network = network;
        this.processHostServerId = processHostServerId;
        this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
    }

    public IReadOnlyList<string> List(string hostname = "") { narrative.EnsureCapability("files.navigation"); return fileSystem.ListFiles(GetTargetHostname(hostname)); }
    public bool Delete(string filename, string hostname = "") { narrative.EnsureCapability("files.manage"); return fileSystem.DeleteFile(GetTargetHostname(hostname), filename); }
    public bool Move(string sourceFilename, string destinationFilename, string hostname = "") { narrative.EnsureCapability("files.manage"); return fileSystem.MoveFile(GetTargetHostname(hostname), sourceFilename, destinationFilename); }
    public bool Copy(string source, string destination, IReadOnlyList<string> files) { narrative.EnsureCapability("files.manage"); return fileSystem.CopyFiles(source, destination, files); }

    private string GetTargetHostname(string hostname) => string.IsNullOrWhiteSpace(hostname) ? network.GetServer(processHostServerId).Hostname : hostname;
}
