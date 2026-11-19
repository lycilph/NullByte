using System.Security.Cryptography;
using System.Text;

namespace NullByte.Editor;

public sealed class EditorToken
{
    private EditorToken(string value, string path) { Value = value; Path = path; }

    public string Value { get; }
    public string Path { get; }

    public static EditorToken LoadOrCreate()
    {
        var directory = System.IO.Path.Combine(AppContext.BaseDirectory, ".nullbyte");
        var path = System.IO.Path.Combine(directory, "editor-token");
        Directory.CreateDirectory(directory);

        if (File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } existing)
            return new EditorToken(existing, path);

        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
        };
        using (var writer = new StreamWriter(path, options))
            writer.Write(value);

        return new EditorToken(value, path);
    }

    public bool Matches(string? candidate)
        => candidate is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(Value));
}