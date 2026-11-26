using NullByte.Core.Domain;

namespace NullByte.Core.Scripting;

public class NullByteFilesApi : INullByteFilesApi
{
    private readonly VirtualFileSystem fileSystem;
    private readonly Network network;
    private readonly ServerId processHostServerId;

    public NullByteFilesApi(VirtualFileSystem fileSystem, Network network, ServerId processHostServerId)
    {
        this.fileSystem = fileSystem;
        this.network = network;
        this.processHostServerId = processHostServerId;
    }

    public IReadOnlyList<string> List(string hostname = "") => fileSystem.ListFiles(GetTargetHostname(hostname));
    
    public bool Delete(string filename, string hostname = "") => fileSystem.DeleteFile(GetTargetHostname(hostname), filename);

    public bool Move(string sourceFilename, string destinationFilename, string hostname = "")
        => fileSystem.MoveFile(GetTargetHostname(hostname), sourceFilename, destinationFilename);

    public bool Copy(string source, string destination, IReadOnlyList<string> files)
        => fileSystem.CopyFiles(source, destination, files);

    private string GetTargetHostname(string hostname) => string.IsNullOrWhiteSpace(hostname) ? network.GetServer(processHostServerId).Hostname : hostname;
}
