using NullByte.Core.Domain;

namespace NullByte.Scripting.Compilation;

public sealed record ScriptIncludeResolutionResult(
    bool Succeeded,
    IReadOnlyList<ScriptCompilationUnit> Units,
    IReadOnlyList<ScriptDiagnostic> Diagnostics)
{
    public static ScriptIncludeResolutionResult Success(IReadOnlyList<ScriptCompilationUnit> units)
        => new(true, units, []);

    public static ScriptIncludeResolutionResult Failure(IReadOnlyList<ScriptDiagnostic> diagnostics)
        => new(false, [], diagnostics);
}

public sealed class ScriptIncludeResolver
{
    private const string MissingIncludeDiagnosticId = "NB0003";
    private const string CycleIncludeDiagnosticId = "NB0004";

    private readonly VirtualFileSystem fileSystem;
    private readonly string hostname;

    public ScriptIncludeResolver(VirtualFileSystem fileSystem, string hostname)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        this.hostname = hostname;
    }

    public ScriptIncludeResolutionResult Resolve(string entryFilename, string entrySource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryFilename);
        ArgumentNullException.ThrowIfNull(entrySource);

        var units = new List<ScriptCompilationUnit>();
        var diagnostics = new List<ScriptDiagnostic>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var activeStack = new List<string>();

        ResolveRecursive(
            filename: entryFilename,
            source: entrySource,
            isEntryPoint: true,
            includeLine: 0,
            includedFrom: null,
            units,
            diagnostics,
            visited,
            activeStack);

        if (diagnostics.Count > 0)
            return ScriptIncludeResolutionResult.Failure(diagnostics);

        return ScriptIncludeResolutionResult.Success(units);
    }

    private void ResolveRecursive(
        string filename,
        string? source,
        bool isEntryPoint,
        int includeLine,
        string? includedFrom,
        List<ScriptCompilationUnit> units,
        List<ScriptDiagnostic> diagnostics,
        HashSet<string> visited,
        List<string> activeStack)
    {
        var activeIndex = FindIndex(activeStack, filename);
        if (activeIndex >= 0)
        {
            var chain = string.Join(" -> ", activeStack.Skip(activeIndex).Append(filename));
            diagnostics.Add(new ScriptDiagnostic(
                CycleIncludeDiagnosticId,
                $"Circular '#include' detected: {chain}.",
                ScriptDiagnosticSeverity.Error,
                Line: includeLine,
                Column: 1));
            return;
        }

        if (visited.Contains(filename))
            return;

        if (source is null)
        {
            if (!fileSystem.TryReadFile(hostname, filename, out source) || source is null)
            {
                diagnostics.Add(new ScriptDiagnostic(
                    MissingIncludeDiagnosticId,
                    includedFrom is null
                        ? $"Script file '{filename}' not found on '{hostname}'."
                        : $"Included script '{filename}' (referenced from '{includedFrom}') not found on '{hostname}'.",
                    ScriptDiagnosticSeverity.Error,
                    Line: includeLine,
                    Column: 1));
                return;
            }
        }

        visited.Add(filename);
        activeStack.Add(filename);

        var parsed = ScriptIncludeParser.ParseDirectives(source, filename);
        diagnostics.AddRange(parsed.Diagnostics);

        foreach (var childInclude in parsed.Includes)
        {
            ResolveRecursive(
                filename: childInclude,
                source: null,
                isEntryPoint: false,
                includeLine: 1,
                includedFrom: filename,
                units,
                diagnostics,
                visited,
                activeStack);
        }

        activeStack.RemoveAt(activeStack.Count - 1);

        // Emit dependencies before dependents so declarations exist when referenced.
        units.Add(new ScriptCompilationUnit(filename, parsed.Source, isEntryPoint));
    }

    private static int FindIndex(List<string> stack, string fileName)
    {
        for (var i = 0; i < stack.Count; i++)
        {
            if (string.Equals(stack[i], fileName, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }
}