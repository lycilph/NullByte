using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using NullByte.Core.Persistence;
using NullByte.Core.Scripting;
using System.Text;

namespace NullByte.Scripting.Compilation;

/// <summary>
/// Compiles player-written top-level scripts into in-memory assemblies that
/// contain one generated <see cref="IScript"/> class.
/// </summary>
public sealed class ScriptCompiler : IScriptCompiler
{
    internal const string DefaultUsingsSource = """
    global using System;
    global using System.Collections.Generic;
    global using System.Linq;
    global using System.Threading;
    global using System.Threading.Tasks;
    global using NullByte.Core.Scripting;
    """;

    private static readonly List<string> allowedAssemblies = new([
        "System.Private.CoreLib.dll",
        "System.Runtime.dll",
        "System.Collections.dll",
        "System.Linq.dll",
        "System.Threading.dll",
        "System.Threading.Tasks.dll"]);

    internal static readonly CSharpParseOptions ParseOptions = new(
        languageVersion: LanguageVersion.Latest,
        documentationMode: DocumentationMode.Parse,
        kind: SourceCodeKind.Regular);

    internal static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        optimizationLevel: OptimizationLevel.Release,
        allowUnsafe: false,
        nullableContextOptions: NullableContextOptions.Enable,
        deterministic: true);

    private readonly IReadOnlyList<MetadataReference> references;

    public ScriptCompiler() : this(CreateScriptReferences()) { }

    internal ScriptCompiler(IReadOnlyList<MetadataReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        this.references = references;
    }

    public ScriptCompilationResult Compile(string source, string filename, IScriptSourceProvider? sources = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        // Find the includes (missing, cyclic and too-deep ones are reported here).
        var graph = new IncludeResolver(sources ?? NoSources.Instance).Resolve(filename, source);
        var diagnostics = new List<ScriptDiagnostic>(graph.Diagnostics);

        // Parse every file with its #include lines blanked, so line numbers stay the player's own.
        var originalText = SourceText.From(IncludeScanner.Blank(source, graph.Root.Directives), Encoding.UTF8);
        var originalTree = CSharpSyntaxTree.ParseText(originalText, ParseOptions, filename);

        var includeTrees = graph.Includes
            .Select(include => CSharpSyntaxTree.ParseText(
                SourceText.From(IncludeScanner.Blank(include.Source, include.Directives), Encoding.UTF8),
                ParseOptions,
                include.Path))
            .ToArray();

        // Syntax errors are reported against the player's own text, in the file they are in.
        foreach (var tree in includeTrees.Prepend(originalTree))
        {
            diagnostics.AddRange(tree.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(ConvertDiagnostic));
        }

        foreach (var tree in includeTrees)
            diagnostics.AddRange(CheckLibrary(tree));

        if (diagnostics.Any(diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error))
            return ScriptCompilationResult.Failure(diagnostics);

        var generatedSource = ScriptSourceGenerator.Generate(
            originalText,
            originalTree.GetCompilationUnitRoot(),
            filename);

        var usingsTree = CSharpSyntaxTree.ParseText(
            SourceText.From(DefaultUsingsSource, Encoding.UTF8),
            ParseOptions,
            "NullByte.GlobalUsings.g.cx");

        var generatedTree = CSharpSyntaxTree.ParseText(
            SourceText.From(generatedSource, Encoding.UTF8),
            ParseOptions,
            "NullByte.Script.g.cx");

        var compilation = CSharpCompilation.Create(
            $"NullByte.Script.{Guid.NewGuid():N}",
            [usingsTree, generatedTree, ..includeTrees],
            references,
            CompilationOptions);

        var ramCost = ScriptRamAnalyzer.Calculate(compilation, [generatedTree, .. includeTrees]);

        using var assemblyStream = new MemoryStream();
        using var symbolsStream = new MemoryStream();

        var emitResult = compilation.Emit(
            peStream: assemblyStream,
            pdbStream: symbolsStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        diagnostics.AddRange(emitResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
            .Select(ConvertDiagnostic)
            .ToArray());

        if (!emitResult.Success)
            return ScriptCompilationResult.Failure(diagnostics);

        return ScriptCompilationResult.Success(
            new CompiledScript(assemblyStream.ToArray(), symbolsStream.ToArray(), ramCost),
            diagnostics);
    }

    internal static IReadOnlyList<MetadataReference> CreateScriptReferences()
    {
        var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
        
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            throw new InvalidOperationException("The trusted platform assemblies could not be determined.");

        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            var fileName = Path.GetFileName(path);
            if (allowedAssemblies.Contains(fileName))
                AddReference(references, path);
        }

        var corePath = typeof(INullByteApi).Assembly.Location;
        var coreXml = Path.ChangeExtension(corePath, ".xml");
        references[corePath] = MetadataReference.CreateFromFile(
            corePath,
            documentation: File.Exists(coreXml) ? XmlDocumentationProvider.CreateFromFile(coreXml) : null);

        return references.Values.ToArray();
    }

    private static void AddReference(Dictionary<string, MetadataReference> references, string assemblyPath)
    {
        if (!references.ContainsKey(assemblyPath))
            references.Add(assemblyPath, MetadataReference.CreateFromFile(assemblyPath));
    }

    private static ScriptDiagnostic ConvertDiagnostic(Diagnostic diagnostic)
    {
        var line = 0;
        var column = 0;
        var endLine = 0;
        var endColumn = 0;
        string? file = null;

        if (diagnostic.Location.IsInSource)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            line = span.StartLinePosition.Line + 1;
            column = span.StartLinePosition.Character + 1;
            endLine = span.EndLinePosition.Line + 1;
            endColumn = span.EndLinePosition.Character + 1;

            // Our own generated wrapper files are not something the player can open.
            file = span.Path.EndsWith(".g.cx", StringComparison.Ordinal) ? null : span.Path;
        }

        return new ScriptDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(),
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => ScriptDiagnosticSeverity.Error,
                DiagnosticSeverity.Warning => ScriptDiagnosticSeverity.Warning,
                _ => ScriptDiagnosticSeverity.Info
            },
            line,
            column,
            file,
            endLine,
            endColumn);
    }

    // A library is usings and type declarations only: no entry point, no nb, nothing that runs.
    private static IEnumerable<ScriptDiagnostic> CheckLibrary(SyntaxTree tree)
    {
        var statement = tree.GetCompilationUnitRoot().Members.OfType<GlobalStatementSyntax>().FirstOrDefault();
        if (statement is null)
            yield break;

        var span = tree.GetLineSpan(statement.Span);
        var position = span.StartLinePosition;
        yield return new ScriptDiagnostic(
            "NB1006",
            "An included file may only contain usings and type declarations. Put methods inside a static class.",
            ScriptDiagnosticSeverity.Error,
            position.Line + 1,
            position.Character + 1,
            tree.FilePath,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    private sealed class NoSources : IScriptSourceProvider
    {
        public static readonly NoSources Instance = new();
        public bool TryReadSource(string path, out string? source) { source = null; return false; }
    }
}