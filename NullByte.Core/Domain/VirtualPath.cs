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
        => TryNormalizeFile(path, out var normalized, out var error)
            ? normalized
            : throw new ArgumentException(error, nameof(path));

    /// <summary>Normalizes the path of a directory. The root is the empty string.</summary>
    public static string NormalizeDirectory(string path)
        => TryNormalizeDirectory(path, out var normalized, out var error)
            ? normalized
            : throw new ArgumentException(error, nameof(path));

    /// <summary>Like <see cref="NormalizeFile"/>, but reports a problem in plain words instead of throwing.</summary>
    public static bool TryNormalizeFile(string path, out string normalized, out string error)
    {
        if (!TryNormalizeDirectory(path, out normalized, out error))
            return false;

        if (normalized.Length == 0)
        {
            error = "A file path cannot be empty.";
            return false;
        }

        return true;
    }

    /// <summary>Like <see cref="NormalizeDirectory"/>, but reports a problem in plain words instead of throwing.</summary>
    public static bool TryNormalizeDirectory(string path, out string normalized, out string error)
    {
        ArgumentNullException.ThrowIfNull(path);

        normalized = "";
        error = "";

        // One leading and one trailing slash are allowed: "/a/b/" means "a/b".
        var trimmed = path.StartsWith('/') ? path[1..] : path;
        if (trimmed.EndsWith('/'))
            trimmed = trimmed[..^1];

        if (trimmed.Length == 0)
            return true;   // the root

        if (trimmed.Length > MaxLength)
        {
            error = $"Paths can be at most {MaxLength} characters long.";
            return false;
        }

        var segments = trimmed.Split('/');
        if (segments.Length > MaxDepth)
        {
            error = $"Paths can be at most {MaxDepth} levels deep.";
            return false;
        }

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." ||
                segment.Contains('\\') ||
                segment.Any(char.IsControl))
            {
                error = "Path segments must be simple names: not empty, not '.' or '..', and without backslashes.";
                return false;
            }
        }

        normalized = trimmed;
        return true;
    }

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
}