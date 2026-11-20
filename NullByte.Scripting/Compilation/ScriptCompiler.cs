using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
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
    public ScriptCompilationResult Compile(string source, string filename)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        var parsed = ScriptIncludeParser.ParseDirectives(source, filename);
        if (parsed.Diagnostics.Count > 0)
            return ScriptCompilationResult.Failure(parsed.Diagnostics);

        return Compile([new ScriptCompilationUnit(filename, parsed.Source, IsEntryPoint: true)]);
    }

    public ScriptCompilationResult Compile(IReadOnlyList<ScriptCompilationUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        if (units.Count == 0)
            throw new ArgumentException("At least one compilation unit is required.", nameof(units));

        var preDiagnostics = new List<ScriptDiagnostic>();
        var parsedUnits = new List<(ScriptCompilationUnit Unit, SourceText Text, CompilationUnitSyntax Root)>(units.Count);

        foreach (var unit in units)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unit.FileName);
            ArgumentNullException.ThrowIfNull(unit.Source);

            var text = SourceText.From(unit.Source, Encoding.UTF8);
            var tree = CSharpSyntaxTree.ParseText(text, ParseOptions, unit.FileName);
            var root = tree.GetCompilationUnitRoot();

            // Syntax errors are reported against each file's own text so line numbers stay accurate.
            var syntaxErrors = tree.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(ConvertDiagnostic);

            preDiagnostics.AddRange(syntaxErrors);

            if (!unit.IsEntryPoint)
                preDiagnostics.AddRange(ScriptIncludeParser.ValidateIncludedScript(root, unit.FileName));

            parsedUnits.Add((unit, text, root));
        }

        if (preDiagnostics.Count > 0)
            return ScriptCompilationResult.Failure(preDiagnostics);

        var usingsTree = CSharpSyntaxTree.ParseText(
            SourceText.From(DefaultUsingsSource, Encoding.UTF8),
            ParseOptions,
            "NullByte.GlobalUsings.g.cx");

        var syntaxTrees = new List<SyntaxTree>(parsedUnits.Count + 1) { usingsTree };
        var generatedTrees = new List<SyntaxTree>(parsedUnits.Count);

        foreach (var (unit, text, root) in parsedUnits)
        {
            var generatedSource = unit.IsEntryPoint
                ? ScriptSourceGenerator.GenerateMapped(text, root, unit.FileName).Text
                : ScriptSourceGenerator.GenerateIncluded(text, root, unit.FileName).Text;

            var generatedTree = CSharpSyntaxTree.ParseText(
                SourceText.From(generatedSource, Encoding.UTF8),
                ParseOptions,
                $"{Path.GetFileNameWithoutExtension(unit.FileName)}.g.cx");

            syntaxTrees.Add(generatedTree);
            generatedTrees.Add(generatedTree);
        }

        var compilation = CSharpCompilation.Create(
            $"NullByte.Script.{Guid.NewGuid():N}",
            syntaxTrees,
            references,
            CompilationOptions);

        var ramCost = ScriptRamAnalyzer.Calculate(
            generatedTrees.Select(tree => (compilation.GetSemanticModel(tree), tree.GetRoot())));

        using var assemblyStream = new MemoryStream();
        using var symbolsStream = new MemoryStream();

        var emitResult = compilation.Emit(
            peStream: assemblyStream,
            pdbStream: symbolsStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        var diagnostics = emitResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
            .Select(ConvertDiagnostic)
            .ToArray();

        if (!emitResult.Success)
            return ScriptCompilationResult.Failure(diagnostics);

        return ScriptCompilationResult.Success(
            new CompiledScript(assemblyStream.ToArray(), symbolsStream.ToArray(), ramCost), diagnostics);
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

        if (diagnostic.Location.IsInSource)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            line = span.StartLinePosition.Line + 1;
            column = span.StartLinePosition.Character + 1;
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
            column);
    }
}