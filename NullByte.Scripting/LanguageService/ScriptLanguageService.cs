using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.LanguageService;

public sealed record ScriptCompletion(string Label, string InsertText, string Kind, string SortText, double? RamCost);

/// <summary>
/// Completion for player scripts. It uses the compiler's wrapper, usings and references,
/// so it only suggests what would actually compile.
/// </summary>
public sealed class ScriptLanguageService : IDisposable
{
    private const string UsingsFileName = "NullByte.GlobalUsings.g.cx";
    private const string ScriptFileName = "NullByte.Script.g.cx";

    private static readonly Assembly[] HostAssemblies = MefHostServices.DefaultAssemblies
        .Append(typeof(CompletionService).Assembly)                        // Microsoft.CodeAnalysis.Features
        .Append(Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"))
        .Distinct()
        .ToArray();

    private readonly AdhocWorkspace workspace;
    private readonly DocumentId scriptId;

    public ScriptLanguageService()
    {
        workspace = new AdhocWorkspace(MefHostServices.Create(HostAssemblies));

        var projectId = ProjectId.CreateNewId();
        var usingsId = DocumentId.CreateNewId(projectId, UsingsFileName);
        scriptId = DocumentId.CreateNewId(projectId, ScriptFileName);

        var project = ProjectInfo.Create(
                projectId, VersionStamp.Create(), "NullByte.Script", "NullByte.Script", LanguageNames.CSharp,
                compilationOptions: ScriptCompiler.CompilationOptions,
                parseOptions: ScriptCompiler.ParseOptions,
                metadataReferences: ScriptCompiler.CreateScriptReferences())
            .WithDocuments([
                DocumentInfo.Create(usingsId, UsingsFileName, loader: Loader(ScriptCompiler.DefaultUsingsSource)),
                DocumentInfo.Create(scriptId, ScriptFileName, loader: Loader(""))
            ]);

        workspace.AddProject(project);
    }

    /// <summary>Pays the one-off start-up cost (MEF composition, JIT) ahead of the first real request.</summary>
    public Task WarmUpAsync() => GetCompletionsAsync("nb.", 3);

    public async Task<IReadOnlyList<ScriptCompletion>> GetCompletionsAsync(string source, int offset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        offset = Math.Clamp(offset, 0, source.Length);

        var text = SourceText.From(source, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(text, ScriptCompiler.ParseOptions, cancellationToken: ct);
        var generated = ScriptSourceGenerator.GenerateMapped(text, tree.GetCompilationUnitRoot(ct), "script.cx");
        var position = generated.ToGenerated(offset);

        // Solutions are immutable, so concurrent requests do not interfere with each other.
        var solution = workspace.CurrentSolution.WithDocumentText(scriptId, SourceText.From(generated.Text, Encoding.UTF8));
        var document = solution.GetDocument(scriptId)!;

        var service = CompletionService.GetService(document)
            ?? throw new InvalidOperationException("Roslyn completion is unavailable (are the Features assemblies present?).");

        var list = await service.GetCompletionsAsync(document, position, CompletionTrigger.Invoke, cancellationToken: ct);
        if (list is null)
            return [];

        var costs = await GetRamCostsAsync(document, position, ct);

        return list.ItemsList
            .Select(item =>
            {
                var kind = item.Tags.FirstOrDefault() ?? "Text";
                double? cost = kind == "Method" && costs.TryGetValue(item.DisplayText, out var c) ? c : null;
                return new ScriptCompletion(item.DisplayText + item.DisplayTextSuffix, item.DisplayText, kind, item.SortText, cost);
            })
            .ToArray();
    }

    /// <summary>For `nb.Files.|`, the RAM cost of each method of the receiver's type, by name.</summary>
    private static async Task<Dictionary<string, double>> GetRamCostsAsync(Document document, int position, CancellationToken ct)
    {
        var costs = new Dictionary<string, double>();

        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);
        if (root is null || model is null)
            return costs;

        // Only member completion shows costs: find the access whose name the cursor is in.
        var access = root.FindToken(Math.Max(position - 1, 0)).Parent?.AncestorsAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault(a => position >= a.OperatorToken.Span.End && position <= a.Name.Span.End);

        if (access is null || model.GetTypeInfo(access.Expression, ct).Type is not { } type)
            return costs;

        var methods = type.GetMembers()
            .Concat(type.AllInterfaces.SelectMany(i => i.GetMembers()))
            .OfType<IMethodSymbol>();

        foreach (var method in methods)
        {
            if (TryGetRamCost(method, out var cost))
                costs[method.Name] = Math.Max(cost, costs.GetValueOrDefault(method.Name));
        }

        return costs;
    }

    private static bool TryGetRamCost(IMethodSymbol method, out double cost)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == typeof(RamCostAttribute).FullName
                && attribute.ConstructorArguments is [{ Value: double value }])
            {
                cost = value;
                return true;
            }
        }

        cost = 0;
        return false;
    }

    private static TextLoader Loader(string text)
        => TextLoader.From(TextAndVersion.Create(SourceText.From(text, Encoding.UTF8), VersionStamp.Create()));

    public void Dispose() => workspace.Dispose();
}