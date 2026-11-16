namespace NullByte.Core.Scripting;

/// <summary>
/// Defines the files sub-API exposed to player-written scripts through the global
/// <c>nb</c> object.
/// </summary>
public interface INullByteFilesApi
{
    /// <summary>Lists the files on a server.</summary>
    [RamCost(0.2)]
    IReadOnlyList<string> List(string hostname = "");

    /// <summary>Deletes a file from a server.</summary>
    [RamCost(0.6)]
    bool Delete(string filename, string hostname = "");

    /// <summary>Renames a file on a server.</summary>
    [RamCost(0.1)]
    bool Move(string sourceFilename, string destinationFilename, string hostname = "");

    /// <summary>Copies files from one server to another.</summary>
    [RamCost(0.6)]
    bool Copy(string source, string destination, IReadOnlyList<string> files);
}