using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NullByte.Scripting.Compilation;

/// <summary>
/// Performs semantic checks for APIs that player scripts must not access.
/// The checks are deliberately based on resolved Roslyn symbols rather than source text.
/// </summary>
internal static class ScriptSecurityAnalyzer
{
    private const string DiagnosticId = "NB1007";

    private static readonly (string Namespace, string Message)[] BannedNamespaces =
    [
        ("System.IO", "File system access is not available to scripts."),
        ("System.Net", "Network access is not available to scripts."),
        ("System.Reflection", "Reflection is not available to scripts."),
        ("System.Runtime.InteropServices", "Native interop is not available to scripts.")
    ];

    private static readonly (string TypeName, string Message)[] BannedTypes =
    [
        ("System.Diagnostics.Process", "Starting or controlling host processes is not available to scripts."),
        ("System.Environment", "Access to the host environment is not available to scripts."),
        ("System.Threading.Thread", "Creating or controlling threads is not available to scripts."),
        ("System.Threading.ThreadPool", "Access to the host thread pool is not available to scripts."),
        ("System.Threading.WaitHandle", "Direct synchronization with host wait handles is not available to scripts.")
    ];

    public static IEnumerable<ScriptDiagnostic> Analyze(CSharpCompilation compilation, IEnumerable<SyntaxTree> trees)
    {
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            var reported = new HashSet<(int Start, int Length, string Message)>();

            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var symbol = GetReferencedSymbol(model, node);
                if (symbol is null || !TryGetBannedMessage(symbol, out var message))
                    continue;

                var location = node.GetLocation();
                var key = (location.SourceSpan.Start, location.SourceSpan.Length, message);
                if (!reported.Add(key))
                    continue;

                var span = location.GetMappedLineSpan();
                yield return new ScriptDiagnostic(
                    DiagnosticId,
                    message,
                    ScriptDiagnosticSeverity.Error,
                    span.StartLinePosition.Line + 1,
                    span.StartLinePosition.Character + 1,
                    tree.FilePath,
                    span.EndLinePosition.Line + 1,
                    span.EndLinePosition.Character + 1);
            }
        }
    }

    private static ISymbol? GetReferencedSymbol(SemanticModel model, SyntaxNode node) => node switch
    {
        IdentifierNameSyntax identifier => model.GetSymbolInfo(identifier).Symbol,
        GenericNameSyntax genericName => model.GetSymbolInfo(genericName).Symbol,
        MemberAccessExpressionSyntax memberAccess => model.GetSymbolInfo(memberAccess).Symbol,
        ObjectCreationExpressionSyntax objectCreation => model.GetSymbolInfo(objectCreation.Type).Symbol,
        _ => null
    };

    private static bool TryGetBannedMessage(ISymbol symbol, out string message)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            var type = current as INamedTypeSymbol ?? current.ContainingType;
            if (type is not null)
            {
                var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty, StringComparison.Ordinal);

                var bannedType = BannedTypes.FirstOrDefault(rule =>
                    string.Equals(typeName, rule.TypeName, StringComparison.Ordinal));
                if (bannedType != default)
                {
                    message = bannedType.Message;
                    return true;
                }
            }

            var namespaceName = current.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                var bannedNamespace = BannedNamespaces.FirstOrDefault(rule =>
                    namespaceName.Equals(rule.Namespace, StringComparison.Ordinal) ||
                    namespaceName.StartsWith(rule.Namespace + ".", StringComparison.Ordinal));
                if (bannedNamespace != default)
                {
                    message = bannedNamespace.Message;
                    return true;
                }
            }
        }

        message = string.Empty;
        return false;
    }
}
