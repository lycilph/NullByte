namespace NullByte.Core.Domain;

/// <summary>
/// Turns what the player typed into a normalized path, relative to the current directory.
/// "/a/b" is absolute, anything else is relative, and "." and ".." are understood here only:
/// the file system never sees them. ".." at the root stays at the root.
/// </summary>
public static class PathResolver
{
    /// <summary>Resolves a path that can be the root. Returns a normalized path ("" is the root).</summary>
    public static bool TryResolve(string currentDirectory, string typed, out string resolved, out string error)
    {
        ArgumentNullException.ThrowIfNull(currentDirectory);
        ArgumentNullException.ThrowIfNull(typed);

        var absolute = typed.StartsWith('/');
        var rest = absolute ? typed[1..] : typed;
        if (rest.EndsWith('/'))
            rest = rest[..^1];

        var segments = new List<string>();
        if (!absolute && currentDirectory.Length > 0)
            segments.AddRange(currentDirectory.Split('/'));

        if (rest.Length > 0)
        {
            foreach (var segment in rest.Split('/'))
            {
                if (segment == ".")
                    continue;

                if (segment == "..")
                {
                    if (segments.Count > 0)
                        segments.RemoveAt(segments.Count - 1);
                    continue;
                }

                segments.Add(segment);   // empty or invalid segments are rejected below
            }
        }

        return VirtualPath.TryNormalizeDirectory(string.Join('/', segments), out resolved, out error);
    }

    /// <summary>Like <see cref="TryResolve"/>, but the result must name something below the root.</summary>
    public static bool TryResolveFile(string currentDirectory, string typed, out string resolved, out string error)
    {
        if (!TryResolve(currentDirectory, typed, out resolved, out error))
            return false;

        if (resolved.Length == 0)
        {
            error = "A file path cannot be empty.";
            return false;
        }

        return true;
    }
}