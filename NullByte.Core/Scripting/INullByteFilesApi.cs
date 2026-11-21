namespace NullByte.Core.Scripting;

/// <summary>
/// Defines the files sub-API exposed to player-written scripts through the global
/// <c>nb</c> object.
/// </summary>
public interface INullByteFilesApi
{
    /// <summary>Lists every file on a server, including those in directories, as paths such as "tools/scan.cx".</summary>
    [RamCost(0.2)]
    IReadOnlyList<string> List(string hostname = "");

    /// <summary>Deletes a file from a server. The filename can be a path such as "tools/scan.cx".</summary>
    [RamCost(0.6)]
    bool Delete(string filename, string hostname = "");

    /// <summary>Moves or renames a file on a server. Missing directories in the destination are created. Returns false if the source is missing or the destination already exists.</summary>
    [RamCost(0.1)]
    bool Move(string sourceFilename, string destinationFilename, string hostname = "");

    /// <summary>Copies files from one server to another, creating missing directories on the destination.</summary>
    [RamCost(0.6)]
    bool Copy(string source, string destination, IReadOnlyList<string> files);
}