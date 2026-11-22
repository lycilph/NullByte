// NullByte.Scripting/Compilation/IncludeResolver.cs
using NullByte.Core.Domain;

namespace NullByte.Scripting.Compilation;

/// <summary>Reads script sources by normalized path. Backed by the virtual file system of one server.</summary>
public interface IScriptSourceProvider
{
    bool TryReadSource(string path, out string? source);
}

internal sealed record ResolvedSource(string Path, string Source, IReadOnlyList<IncludeDirective> Directives);

/// <summary>The root script and everything it includes: dependencies first, each file once.</summary>
internal sealed record IncludeGraph(
    ResolvedSource Root,
    IReadOnlyList<ResolvedSource> Includes,
    IReadOnlyList<ScriptDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == ScriptDiagnosticSeverity.Error);
}

internal sealed class IncludeResolver(IScriptSourceProvider provider)
{
    public const int MaxDepth = 8;

    /// <param name="rootPath">Already normalized (see <see cref="VirtualPath"/>).</param>
    public IncludeGraph Resolve(string rootPath, string rootSource)
    {
        var diagnostics = new List<ScriptDiagnostic>();
        var includes = new List<ResolvedSource>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { rootPath };
        var stack = new List<string> { rootPath };

        var directives = Visit(rootPath, rootSource, stack, visited, includes, diagnostics);
        return new IncludeGraph(new ResolvedSource(rootPath, rootSource, directives), includes, diagnostics);
    }

    private IReadOnlyList<IncludeDirective> Visit(string path, string source, List<string> stack,
        HashSet<string> visited, List<ResolvedSource> output, List<ScriptDiagnostic> diagnostics)
    {
        var directives = IncludeScanner.Scan(source, path, diagnostics);

        foreach (var directive in directives)
        {
            ScriptDiagnostic Error(string id, string message)
                => new(id, message, ScriptDiagnosticSeverity.Error, directive.Line, directive.Column, path);

            if (!VirtualPath.TryNormalizeFile(directive.Path, out var target, out var pathError))
            {
                diagnostics.Add(Error("NB1004", $"Invalid include path '{directive.Path}': {pathError}"));
                continue;
            }

            var cycleStart = stack.IndexOf(target);
            if (cycleStart >= 0)
            {
                var chain = string.Join(" -> ", stack.Skip(cycleStart).Append(target));
                diagnostics.Add(Error("NB1002", $"Include cycle: {chain}"));
                continue;
            }

            if (!visited.Add(target))
                continue;   // already included: once is enough

            if (stack.Count > MaxDepth)
            {
                diagnostics.Add(Error("NB1003", $"Includes are nested too deeply (limit {MaxDepth}): '{target}'"));
                continue;
            }

            if (!provider.TryReadSource(target, out var text) || text is null)
            {
                diagnostics.Add(Error("NB1001", $"Include not found: '{target}'"));
                continue;
            }

            stack.Add(target);
            var childDirectives = Visit(target, text, stack, visited, output, diagnostics);
            stack.RemoveAt(stack.Count - 1);

            output.Add(new ResolvedSource(target, text, childDirectives));   // after its own includes
        }

        return directives;
    }
}