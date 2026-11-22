namespace NullByte.Scripting.Compilation;

/// <summary>The sources a script needs, copied out on the game thread so compiling can happen anywhere.</summary>
public sealed class SourceSnapshot : IScriptSourceProvider
{
    private readonly Dictionary<string, string> files;

    private SourceSnapshot(Dictionary<string, string> files) => this.files = files;

    public static SourceSnapshot Capture(IScriptSourceProvider live, string rootPath, string rootSource)
    {
        var graph = new IncludeResolver(live).Resolve(rootPath, rootSource);
        return new SourceSnapshot(graph.Includes.ToDictionary(i => i.Path, i => i.Source, StringComparer.Ordinal));
    }

    // Anything missing here is simply reported as "include not found" by the compiler.
    public bool TryReadSource(string path, out string? source) => files.TryGetValue(path, out source);

    /// <summary>True if the source has at least one well-formed #include.</summary>
    public static bool UsesIncludes(string source)
        => IncludeScanner.Scan(source, string.Empty, []).Count > 0;
}