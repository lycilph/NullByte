using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using NullByte.Core.Scripting;

namespace NullByte.Scripting.Compilation;

/// <summary>
/// Compiles player-written top-level scripts into in-memory assemblies that
/// contain one generated <see cref="IScript"/> class.
/// </summary>
public sealed class ScriptCompiler : IScriptCompiler
{
    private const string DefaultUsingsSource = """
    global using System;
    global using System.Collections.Generic;
    global using System.Linq;
    global using System.Threading;
    global using System.Threading.Tasks;
    """;

    private static readonly List<string> allowedAssemblies = new([
        "System.Private.CoreLib.dll",
        "System.Runtime.dll",
        "System.Collections.dll",
        "System.Linq.dll",
        "System.Threading.dll",
        "System.Threading.Tasks.dll"]);

    private static readonly CSharpParseOptions ParseOptions = new(
        languageVersion: LanguageVersion.Latest,
        documentationMode: DocumentationMode.Parse,
        kind: SourceCodeKind.Regular);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
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

    public ScriptCompilationResult Compile(string source, string fileName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var originalText = SourceText.From(source, Encoding.UTF8);
        var originalTree = CSharpSyntaxTree.ParseText(originalText, ParseOptions, fileName);

        // Syntax errors are reported against the player's own text, so the
        // line numbers are right and the generator never sees broken code.
        var syntaxErrors = originalTree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(ConvertDiagnostic)
            .ToArray();

        if (syntaxErrors.Length > 0)
            return ScriptCompilationResult.Failure(syntaxErrors);

        var generatedSource = ScriptSourceGenerator.Generate(
            originalText,
            originalTree.GetCompilationUnitRoot(),
            fileName);

        var usingsTree = CSharpSyntaxTree.ParseText(
            SourceText.From(DefaultUsingsSource, Encoding.UTF8),
            ParseOptions,
            "NullByte.GlobalUsings.g.cs");

        var generatedTree = CSharpSyntaxTree.ParseText(
            SourceText.From(generatedSource, Encoding.UTF8),
            ParseOptions,
            "NullByte.Script.g.cs");

        var compilation = CSharpCompilation.Create(
            $"NullByte.Script.{Guid.NewGuid():N}",
            [usingsTree, generatedTree],
            references,
            CompilationOptions);

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
            new CompiledScript(assemblyStream.ToArray(), symbolsStream.ToArray()),
            diagnostics);
    }

    private static IReadOnlyList<MetadataReference> CreateScriptReferences()
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

        AddReference(references, typeof(INullByteApi).Assembly.Location);

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