using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Text;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.LanguageService;

public sealed record ScriptCompletion(string Label, string InsertText, string Kind, string SortText, double? RamCost, string? Documentation);
public sealed record ScriptHover(string Signature, string? Documentation, double? RamCost);
public sealed record ApiMember(double RamCost, string? Summary);

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
    
    private (Document Document, int Position) Prepare(string source, int offset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        offset = Math.Clamp(offset, 0, source.Length);

        var text = SourceText.From(source, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(text, ScriptCompiler.ParseOptions, cancellationToken: ct);
        var generated = ScriptSourceGenerator.GenerateMapped(text, tree.GetCompilationUnitRoot(ct), "script.cx");

        // Solutions are immutable, so concurrent requests do not interfere with each other.
        var solution = workspace.CurrentSolution.WithDocumentText(scriptId, SourceText.From(generated.Text, Encoding.UTF8));
        return (solution.GetDocument(scriptId)!, generated.ToGenerated(offset));
    }
    public async Task<ScriptHover?> GetHoverAsync(string source, int offset, CancellationToken ct = default)
    {
        var (document, position) = Prepare(source, offset, ct);

        var quickInfo = QuickInfoService.GetService(document);
        if (quickInfo is null)
            return null;

        var item = await quickInfo.GetQuickInfoAsync(document, position, ct);
        if (item is null)
            return null;

        var signature = SectionText(item, QuickInfoSectionKinds.Description);
        if (signature.Length == 0)
            return null;

        var documentation = SectionText(item, QuickInfoSectionKinds.DocumentationComments);

        double? cost = null;
        if (await SymbolFinder.FindSymbolAtPositionAsync(document, position, ct) is IMethodSymbol method
            && TryGetRamCost(method, out var methodCost))
            cost = methodCost;

        return new ScriptHover(signature, documentation.Length == 0 ? null : documentation, cost);
    }

    private static string SectionText(QuickInfoItem item, string kind)
        => string.Concat(item.Sections.Where(s => s.Kind == kind).SelectMany(s => s.TaggedParts).Select(p => p.Text)).Trim();

    public async Task<IReadOnlyList<ScriptCompletion>> GetCompletionsAsync(string source, int offset, CancellationToken ct = default)
    {
        var (document, position) = Prepare(source, offset, ct);

        var service = CompletionService.GetService(document)
            ?? throw new InvalidOperationException("Roslyn completion is unavailable (are the Features assemblies present?).");

        var list = await service.GetCompletionsAsync(document, position, CompletionTrigger.Invoke, cancellationToken: ct);
        if (list is null)
            return [];

        var members = await GetApiMembersAsync(document, position, ct);

        return list.ItemsList
            .Select(item =>
            {
                var kind = item.Tags.FirstOrDefault() ?? "Text";
                var member = kind == "Method" && members.TryGetValue(item.DisplayText, out var m) ? m : null;
                return new ScriptCompletion(
                    item.DisplayText + item.DisplayTextSuffix, item.DisplayText, kind, item.SortText,
                    member?.RamCost, member?.Summary);
            })
            .ToArray();
    }

    /// <summary>For `nb.Files.|`, the RAM cost of each method of the receiver's type, by name.</summary>
    private static async Task<Dictionary<string, ApiMember>> GetApiMembersAsync(Document document, int position, CancellationToken ct)
    {
        var members = new Dictionary<string, ApiMember>();

        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);
        if (root is null || model is null)
            return members;

        // Only member completion shows costs: find the access whose name the cursor is in.
        var access = root.FindToken(Math.Max(position - 1, 0)).Parent?.AncestorsAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault(a => position >= a.OperatorToken.Span.End && position <= a.Name.Span.End);

        if (access is null || model.GetTypeInfo(access.Expression, ct).Type is not { } type)
            return members;

        var methods = type.GetMembers()
            .Concat(type.AllInterfaces.SelectMany(i => i.GetMembers()))
            .OfType<IMethodSymbol>();

        foreach (var method in methods)
        {
            if (!TryGetRamCost(method, out var cost))
                continue;

            members[method.Name] = members.TryGetValue(method.Name, out var existing)
                ? new ApiMember(Math.Max(cost, existing.RamCost), existing.Summary ?? Summary(method))
                : new ApiMember(cost, Summary(method));
        }

        return members;
    }

    private static string? Summary(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var summary = XDocument.Parse(xml).Descendants("summary").FirstOrDefault();
            if (summary is null)
                return null;

            foreach (var reference in summary.Descendants().Where(e => e.Name == "see" || e.Name == "paramref").ToList())
            {
                var name = reference.Attribute("cref")?.Value.Split('.').Last()
                    ?? reference.Attribute("name")?.Value
                    ?? reference.Attribute("langword")?.Value
                    ?? "";
                reference.ReplaceWith(name);
            }

            var text = Regex.Replace(summary.Value, @"\s+", " ").Trim();
            return text.Length == 0 ? null : text;
        }
        catch (XmlException)
        {
            return null;
        }
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