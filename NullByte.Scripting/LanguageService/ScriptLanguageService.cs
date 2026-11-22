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

public sealed record ScriptParameterHelp(string Label, string? Documentation);
public sealed record ScriptSignature(string Label, string? Documentation, double? RamCost, int ActiveParameter, IReadOnlyList<ScriptParameterHelp> Parameters);
public sealed record ScriptSignatureHelp(IReadOnlyList<ScriptSignature> Signatures, int ActiveSignature);
/// <summary>Where something is declared. A null File means the script that was asked about.</summary>
public sealed record ScriptDefinition(string? File, int Line, int Column);
public sealed record ScriptCompletion(string Label, string InsertText, string Kind, string SortText, double? RamCost, string? Documentation);
public sealed record ScriptHover(string Signature, string? Documentation, double? RamCost);
/// <summary>Where a script lives and how to read the files it includes.</summary>
public sealed record IncludeContext(string FileName, IScriptSourceProvider Sources);

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
    public async Task WarmUpAsync()
    {
        await GetCompletionsAsync("nb.", 3);
        await GetHoverAsync("nb.GetHackingLevel();", 5);
        await GetSignatureHelpAsync("nb.Hack(", 8);
    }

    public async Task<ScriptDefinition?> GetDefinitionAsync(
        string source, int offset, CancellationToken ct = default, IncludeContext? context = null)
    {
        var (document, position) = Prepare(source, offset, context, ct);

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position, ct);
        var location = symbol?.Locations.FirstOrDefault(l => l.IsInSource);
        if (location?.SourceTree is not { } tree)
            return null;   // the game's API and the framework have no source to jump to

        var span = location.GetMappedLineSpan();
        var line = span.StartLinePosition.Line + 1;
        var column = span.StartLinePosition.Character + 1;

        if (tree == await document.GetSyntaxTreeAsync(ct))
            return span.HasMappedPath ? new ScriptDefinition(null, line, column) : null;   // null: generated wrapper (nb, args)

        return string.IsNullOrEmpty(tree.FilePath) ? null : new ScriptDefinition(tree.FilePath, line, column);
    }

    private (Document Document, int Position) Prepare(string source, int offset, IncludeContext? context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        offset = Math.Clamp(offset, 0, source.Length);

        // Problems with the includes themselves (missing, cyclic) are the diagnostics' business;
        // completion just uses whatever resolved.
        IReadOnlyList<IncludeDirective> directives;
        IReadOnlyList<ResolvedSource> includes = [];

        if (context is null)
            directives = IncludeScanner.Scan(source, string.Empty, []);
        else
        {
            var graph = new IncludeResolver(context.Sources).Resolve(context.FileName, source);
            directives = graph.Root.Directives;
            includes = graph.Includes;
        }

        // Blanking keeps every offset the editor sent valid.
        var text = SourceText.From(IncludeScanner.Blank(source, directives), Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(text, ScriptCompiler.ParseOptions, cancellationToken: ct);
        var generated = ScriptSourceGenerator.GenerateMapped(text, tree.GetCompilationUnitRoot(ct), "script.cx");

        // Solutions are immutable, so concurrent requests do not interfere with each other.
        var solution = workspace.CurrentSolution.WithDocumentText(scriptId, SourceText.From(generated.Text, Encoding.UTF8));

        foreach (var include in includes)
        {
            solution = solution.AddDocument(
                DocumentId.CreateNewId(scriptId.ProjectId, include.Path),
                include.Path,
                SourceText.From(IncludeScanner.Blank(include.Source, include.Directives), Encoding.UTF8),
                filePath: include.Path);
        }

        return (solution.GetDocument(scriptId)!, generated.ToGenerated(offset));
    }

    public async Task<ScriptHover?> GetHoverAsync(string source, int offset, CancellationToken ct = default, IncludeContext? context = null)
    {
        var (document, position) = Prepare(source, offset, context, ct);

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

    public async Task<IReadOnlyList<ScriptCompletion>> GetCompletionsAsync(string source, int offset, CancellationToken ct = default, IncludeContext? context = null)
    {
        var (document, position) = Prepare(source, offset, context, ct);

        var service = CompletionService.GetService(document)
            ?? throw new InvalidOperationException("Roslyn completion is unavailable (are the Features assemblies present?).");

        var list = await service.GetCompletionsAsync(document, position, CompletionTrigger.Invoke, cancellationToken: ct);
        if (list is null)
            return [];

        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);

        var objectMemberNames = GetSystemObjectMemberNames(root, model, position, ct);
        var members = await GetApiMembersAsync(document, position, ct);

        return list.ItemsList
            .Where(item => !(item.Tags.FirstOrDefault() == "Method" && objectMemberNames.Contains(item.DisplayText)))
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

    private static HashSet<string> GetSystemObjectMemberNames(SyntaxNode? root, SemanticModel? model, int position, CancellationToken ct)
    {
        if (root is null || model is null)
            return [];

        var access = root.FindToken(Math.Max(position - 1, 0)).Parent?.AncestorsAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault(a => position >= a.OperatorToken.Span.End && position <= a.Name.Span.End);

        if (access is null || model.GetTypeInfo(access.Expression, ct).Type is null)
            return [];

        var systemObject = model.Compilation.GetSpecialType(SpecialType.System_Object);
        return systemObject.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !TryGetRamCost(m, out _))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);
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

    public async Task<ScriptSignatureHelp?> GetSignatureHelpAsync(string source, int offset, CancellationToken ct = default, IncludeContext? context = null)
    {
        var (document, position) = Prepare(source, offset, context, ct);

        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);
        if (root is null || model is null)
            return null;

        // The innermost call whose parentheses contain the cursor.
        var arguments = root.FindToken(Math.Max(position - 1, 0)).Parent?.AncestorsAndSelf()
            .OfType<ArgumentListSyntax>()
            .FirstOrDefault(a => a.Parent is InvocationExpressionSyntax or ObjectCreationExpressionSyntax
                && position >= a.OpenParenToken.Span.End
                && (a.CloseParenToken.IsMissing || position <= a.CloseParenToken.SpanStart));

        if (arguments?.Parent is not { } call)
            return null;

        var candidates = GetCandidates(model, call, position, ct);
        if (candidates.Count == 0)
            return null;

        // Commas before the cursor tell us which argument we are on.
        var argumentIndex = arguments.Arguments.GetSeparators().Count(s => s.Span.End <= position);

        // Prefer the overload the compiler resolved, else the first one with room for this argument.
        var resolved = model.GetSymbolInfo(call, ct).Symbol as IMethodSymbol;
        var active = candidates.FindIndex(c => resolved is not null && SymbolEqualityComparer.Default.Equals(c, resolved));
        if (active < 0)
            active = candidates.FindIndex(c => c.Parameters.Length > argumentIndex || HasParams(c));
        if (active < 0)
            active = 0;

        return new ScriptSignatureHelp(
            candidates.Select(c => ToSignature(c, arguments, argumentIndex)).ToArray(), active);
    }

    private static List<IMethodSymbol> GetCandidates(SemanticModel model, SyntaxNode call, int position, CancellationToken ct)
    {
        IEnumerable<IMethodSymbol> methods = call switch
        {
            InvocationExpressionSyntax invocation => MethodGroup(model, invocation, ct),
            ObjectCreationExpressionSyntax creation when model.GetTypeInfo(creation, ct).Type is INamedTypeSymbol type
                => type.InstanceConstructors,
            _ => []
        };

        return methods.Where(m => model.IsAccessible(position, m)).OrderBy(m => m.Parameters.Length).ToList();
    }

    private static IEnumerable<IMethodSymbol> MethodGroup(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken ct)
    {
        var group = model.GetMemberGroup(invocation.Expression, ct).OfType<IMethodSymbol>().ToList();
        if (group.Count > 0)
            return group;

        var info = model.GetSymbolInfo(invocation, ct);   // delegates and local functions
        return info.Symbol is IMethodSymbol method ? [method] : info.CandidateSymbols.OfType<IMethodSymbol>();
    }

    private static bool HasParams(IMethodSymbol method)
        => method.Parameters.Length > 0 && method.Parameters[^1].IsParams;

    private static ScriptSignature ToSignature(IMethodSymbol method, ArgumentListSyntax arguments, int argumentIndex)
    {
        var documentation = DocumentationRoot((method.ReducedFrom ?? method).OriginalDefinition);

        var parameters = method.Parameters
            .Select(p => new ScriptParameterHelp(
                p.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                Flatten(documentation?.Elements("param").FirstOrDefault(e => (string?)e.Attribute("name") == p.Name))))
            .ToArray();

        var isConstructor = method.MethodKind == MethodKind.Constructor;
        var name = isConstructor
            ? method.ContainingType.Name
            : method.Name + (method.TypeParameters.Length > 0 ? $"<{string.Join(", ", method.TypeParameters.Select(t => t.Name))}>" : "");
        var returnType = isConstructor ? "" : method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + " ";

        return new ScriptSignature(
            $"{returnType}{name}({string.Join(", ", parameters.Select(p => p.Label))})",
            Flatten(documentation?.Element("summary")),
            TryGetRamCost(method, out var cost) ? cost : null,
            ActiveParameter(method, arguments, argumentIndex),
            parameters);
    }

    private static int ActiveParameter(IMethodSymbol method, ArgumentListSyntax arguments, int argumentIndex)
    {
        // A named argument ("hostname: ...") points at its own parameter.
        if (arguments.Arguments.ElementAtOrDefault(argumentIndex)?.NameColon is { } named)
        {
            var index = method.Parameters.Select(p => p.Name).ToList().IndexOf(named.Name.Identifier.ValueText);
            if (index >= 0)
                return index;
        }

        // Extra arguments all belong to a params array.
        return HasParams(method) ? Math.Min(argumentIndex, method.Parameters.Length - 1) : argumentIndex;
    }

    private static XElement? DocumentationRoot(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try { return XDocument.Parse(xml).Root; }
        catch (XmlException) { return null; }
    }

    /// <summary>Plain text of a doc element: whitespace flattened, see/paramref turned into words.</summary>
    private static string? Flatten(XElement? element)
    {
        if (element is null)
            return null;

        foreach (var reference in element.Descendants().Where(e => e.Name == "see" || e.Name == "paramref").ToList())
        {
            var name = reference.Attribute("cref")?.Value.Split('.').Last()
                ?? reference.Attribute("name")?.Value
                ?? reference.Attribute("langword")?.Value
                ?? "";
            reference.ReplaceWith(name);
        }

        var text = Regex.Replace(element.Value, @"\s+", " ").Trim();
        return text.Length == 0 ? null : text;
    }

    private static string? Summary(ISymbol symbol) => Flatten(DocumentationRoot(symbol)?.Element("summary"));

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