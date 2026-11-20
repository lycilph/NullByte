namespace NullByte.Core.Domain;

/// <summary>
/// Paths inside a server's virtual file system: "/"-separated, relative to the server root,
/// case-sensitive. Normalized paths have no leading or trailing slash, and the root is "".
/// </summary>
public static class VirtualPath
{
    public const int MaxDepth = 8;
    public const int MaxLength = 256;

    /// <summary>Normalizes the path of a file. The root is not a file.</summary>
    public static string NormalizeFile(string path)
    {
        var normalized = Normalize(path);
        if (normalized.Length == 0)
            throw new ArgumentException("A file path cannot be empty.", nameof(path));

        return normalized;
    }

    /// <summary>Normalizes the path of a directory. The root is the empty string.</summary>
    public static string NormalizeDirectory(string path) => Normalize(path);

    /// <summary>The directory containing a path ("" for the root), or null for the root itself.</summary>
    public static string? Parent(string normalizedPath)
    {
        if (normalizedPath.Length == 0)
            return null;

        var index = normalizedPath.LastIndexOf('/');
        return index < 0 ? "" : normalizedPath[..index];
    }

    /// <summary>The last segment of a path.</summary>
    public static string Name(string normalizedPath)
    {
        var index = normalizedPath.LastIndexOf('/');
        return index < 0 ? normalizedPath : normalizedPath[(index + 1)..];
    }

    /// <summary>The directories above a path, outermost first, excluding the root: "a/b/c.cx" gives "a", "a/b".</summary>
    public static IEnumerable<string> Ancestors(string normalizedPath)
    {
        var index = normalizedPath.IndexOf('/');
        while (index >= 0)
        {
            yield return normalizedPath[..index];
            index = normalizedPath.IndexOf('/', index + 1);
        }
    }

    private static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // One leading and one trailing slash are allowed: "/a/b/" means "a/b".
        var trimmed = path.StartsWith('/') ? path[1..] : path;
        if (trimmed.EndsWith('/'))
            trimmed = trimmed[..^1];

        if (trimmed.Length == 0)
            return "";

        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Paths can be at most {MaxLength} characters long.", nameof(path));

        var segments = trimmed.Split('/');
        if (segments.Length > MaxDepth)
            throw new ArgumentException($"Paths can be at most {MaxDepth} levels deep.", nameof(path));

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." ||
                segment.Contains('\\') ||
                segment.Any(char.IsControl))
            {
                throw new ArgumentException(
                    "Path segments must be simple names: not empty, not '.' or '..', and without backslashes.",
                    nameof(path));
            }
        }

        return trimmed;
    }
}