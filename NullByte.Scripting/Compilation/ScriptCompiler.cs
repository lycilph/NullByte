using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using NullByte.Core.Scripting;
using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Compilation;

/// <summary>
/// Compiles player-written top-level C# programs into in-memory assemblies.
/// </summary>
public sealed class ScriptCompiler : IScriptCompiler
{
    private const string GeneratedFileName = "NullByte.Script.g.cs";

    private const string DefaultUsingsSource = """
    global using System;
    global using System.Collections.Generic;
    global using System.Linq;
    global using System.Threading;
    global using System.Threading.Tasks;
    """;

    private static readonly CSharpParseOptions ParseOptions = new(
        languageVersion: LanguageVersion.Latest,
        documentationMode: DocumentationMode.Parse,
        kind: SourceCodeKind.Regular);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.ConsoleApplication,
        optimizationLevel: OptimizationLevel.Release,
        allowUnsafe: false,
        nullableContextOptions: NullableContextOptions.Enable,
        deterministic: true);

    private readonly IReadOnlyList<MetadataReference> references;

    public ScriptCompiler() : this(CreateDefaultReferences()) {}

    internal ScriptCompiler(IReadOnlyList<MetadataReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        this.references = references;
    }

    public ScriptCompilationResult Compile(string source, string fileName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var originalSource = SourceText.From(
            source,
            Encoding.UTF8);

        var originalTree = CSharpSyntaxTree.ParseText(
            originalSource,
            ParseOptions,
            fileName);

        var originalRoot = originalTree.GetCompilationUnitRoot();

        var generatedSource = GenerateSource(
            originalSource,
            originalRoot,
            fileName);
        
        var defaultUsingsTree = CSharpSyntaxTree.ParseText(
            SourceText.From(DefaultUsingsSource, Encoding.UTF8),
            ParseOptions,
            "NullByte.GlobalUsings.g.cs");

        var generatedTree = CSharpSyntaxTree.ParseText(
            SourceText.From(generatedSource, Encoding.UTF8),
            ParseOptions,
            GeneratedFileName);

        var assemblyName = $"NullByte.Script.{Guid.NewGuid():N}";

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [defaultUsingsTree, generatedTree],
            references,
            CompilationOptions);

        using var assemblyStream = new MemoryStream();
        using var symbolsStream = new MemoryStream();

        var emitOptions = new EmitOptions(
            debugInformationFormat: DebugInformationFormat.PortablePdb);

        var emitResult = compilation.Emit(
            peStream: assemblyStream,
            pdbStream: symbolsStream,
            options: emitOptions);

        var diagnostics = emitResult.Diagnostics
            .Where(IsVisibleDiagnostic)
            .Select(ConvertDiagnostic)
            .ToArray();

        if (!emitResult.Success)
            return ScriptCompilationResult.Failure(diagnostics);

        var compiledScript = new CompiledScript(
            assemblyStream.ToArray(),
            symbolsStream.ToArray());

        return ScriptCompilationResult.Success(
            compiledScript,
            diagnostics);
    }

    private static string GenerateSource(SourceText originalSource, CompilationUnitSyntax originalRoot, string fileName)
    {
        var insertionPosition = FindInsertionPosition(originalRoot);

        var sourceBeforeInsertion = originalSource
            .ToString(TextSpan.FromBounds(0, insertionPosition));

        var sourceAfterInsertion = originalSource
            .ToString(TextSpan.FromBounds(insertionPosition, originalSource.Length));

        var sourceLine = originalSource.Lines
            .GetLineFromPosition(insertionPosition)
            .LineNumber + 1;

        var builder = new StringBuilder();

        builder.Append(sourceBeforeInsertion);

        if (builder.Length > 0 &&
            builder[^1] != '\n' &&
            builder[^1] != '\r')
        {
            builder.AppendLine();
        }

        builder.AppendLine("#line hidden");
        builder.AppendLine(
            "var nb = global::NullByte.Scripting.Runtime." +
            "ScriptRuntime.CurrentApi;");

        builder.Append("#line ");
        builder.Append(sourceLine);
        builder.Append(" \"");
        builder.Append(EscapeFileName(fileName));
        builder.AppendLine("\"");

        builder.Append(sourceAfterInsertion);

        return builder.ToString();
    }

    private static int FindInsertionPosition(CompilationUnitSyntax root)
    {
        if (root.Usings.Count > 0)
        {
            return root.Usings[^1].FullSpan.End;
        }

        if (root.AttributeLists.Count > 0)
        {
            return root.AttributeLists[^1].FullSpan.End;
        }

        return 0;
    }

    private static string EscapeFileName(string fileName)
    {
        return fileName
            .Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
            .Replace(
                "\"",
                "\\\"",
                StringComparison.Ordinal);
    }

    private static IReadOnlyList<MetadataReference> CreateDefaultReferences()
    {
        var references =
            new Dictionary<string, MetadataReference>(
                StringComparer.OrdinalIgnoreCase);

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            throw new InvalidOperationException("The trusted platform assemblies could not be determined.");
        }

        foreach (var assemblyPath in
                 trustedPlatformAssemblies.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            AddReference(references, assemblyPath);
        }

        AddReference(references, typeof(INullByteApi).Assembly);
        AddReference(references, typeof(ScriptRuntime).Assembly);

        return references.Values.ToArray();
    }

    private static void AddReference(IDictionary<string, MetadataReference> references, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly.Location))
            throw new InvalidOperationException($"Assembly '{assembly.FullName}' does not have a file location.");

        AddReference(references, assembly.Location);
    }

    private static void AddReference(IDictionary<string, MetadataReference> references, string assemblyPath)
    {
        if (references.ContainsKey(assemblyPath))
            return;

        references.Add(assemblyPath, MetadataReference.CreateFromFile(assemblyPath));
    }

    private static ScriptDiagnostic ConvertDiagnostic(Diagnostic diagnostic)
    {
        var line = 0;
        var column = 0;

        if (diagnostic.Location.IsInSource)
        {
            var mappedSpan = diagnostic.Location
                .GetMappedLineSpan();

            line = mappedSpan.StartLinePosition.Line + 1;
            column = mappedSpan.StartLinePosition.Character + 1;
        }

        return new ScriptDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(),
            ConvertSeverity(diagnostic.Severity),
            line,
            column);
    }

    private static ScriptDiagnosticSeverity ConvertSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => ScriptDiagnosticSeverity.Error,
            DiagnosticSeverity.Warning => ScriptDiagnosticSeverity.Warning,
            _ => ScriptDiagnosticSeverity.Info
        };
    }

    private static bool IsVisibleDiagnostic(Diagnostic diagnostic)
        => diagnostic.Severity != DiagnosticSeverity.Hidden;
}