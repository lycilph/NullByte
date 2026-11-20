using System.Text.Json;

namespace NullByte.Editor;

/// <summary>Tells the VS Code extension where the game listens and how to authenticate.</summary>
internal static class EditorConnectionFile
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nullbyte", "editor.json");

    public static void Write(string path, string url, string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        // Write beside the target and move it into place, so the extension never reads half a file.
        var temp = path + ".tmp";
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
        };
        using (var writer = new StreamWriter(temp, options))
            writer.Write(JsonSerializer.Serialize(new { url, token, pid = Environment.ProcessId }));

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Deletes the file, unless another game instance has replaced it since.</summary>
    public static void RemoveIfOwned(string path, string token)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("token", out var value) && value.GetString() == token)
                File.Delete(path);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Already gone, or not ours to touch.
        }
    }
}