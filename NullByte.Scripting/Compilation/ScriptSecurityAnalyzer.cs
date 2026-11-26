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
    private const string SecurityDiagnosticId = "NB1007";
    private const string BlockingDiagnosticId = "NB1008";
    private const string CooperativeLoopDiagnosticId = "NB1009";

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

    private static readonly (string Namespace, string TypeMetadataName, string MemberName)[] BlockingMembers =
    [
        ("System.Threading.Tasks", "Task", "Wait"),
        ("System.Threading.Tasks", "Task", "WaitAll"),
        ("System.Threading.Tasks", "Task", "WaitAny"),
        ("System.Threading.Tasks", "Task`1", "Result"),
        ("System.Threading.Tasks", "ValueTask`1", "Result"),
        ("System.Threading", "Monitor", "Wait"),
        ("System.Threading", "SemaphoreSlim", "Wait"),
        ("System.Threading", "ManualResetEventSlim", "Wait"),
        ("System.Threading", "CountdownEvent", "Wait"),
        ("System.Threading", "Barrier", "SignalAndWait"),
        ("System.Threading", "SpinWait", "SpinUntil"),
        ("System.Threading", "WaitHandle", "WaitOne"),
        ("System.Threading", "WaitHandle", "WaitAny"),
        ("System.Threading", "WaitHandle", "WaitAll")
    ];

    public static IEnumerable<ScriptDiagnostic> Analyze(CSharpCompilation compilation, IEnumerable<SyntaxTree> trees)
    {
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            var reported = new HashSet<(int Start, int Length, string Id)>();

            foreach (var loop in tree.GetRoot().DescendantNodes().OfType<StatementSyntax>().Where(IsLoopStatement))
            {
                if (ContainsAwait(loop))
                    continue;

                var span = loop.GetLocation().GetMappedLineSpan();
                yield return new ScriptDiagnostic(
                    CooperativeLoopDiagnosticId,
                    "Loops must await an asynchronous operation so scripts can yield control. Use an asynchronous game API or await nb.Sleep(...).",
                    ScriptDiagnosticSeverity.Error,
                    span.StartLinePosition.Line + 1,
                    span.StartLinePosition.Character + 1,
                    tree.FilePath,
                    span.EndLinePosition.Line + 1,
                    span.EndLinePosition.Character + 1);
            }

            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var symbol = GetReferencedSymbol(model, node);
                if (symbol is null || !TryGetDiagnostic(symbol, out var diagnosticId, out var message))
                    continue;

                var location = node.GetLocation();
                var key = (location.SourceSpan.Start, location.SourceSpan.Length, diagnosticId);
                if (!reported.Add(key))
                    continue;

                var span = location.GetMappedLineSpan();
                yield return new ScriptDiagnostic(
                    diagnosticId,
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

    private static bool IsLoopStatement(StatementSyntax statement) => statement is
        WhileStatementSyntax or
        DoStatementSyntax or
        ForStatementSyntax or
        ForEachStatementSyntax or
        ForEachVariableStatementSyntax;

    private static bool ContainsAwait(StatementSyntax loop) =>
        loop.DescendantNodes(descendIntoChildren: node =>
                node == loop ||
                node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<AwaitExpressionSyntax>()
            .Any();

    private static ISymbol? GetReferencedSymbol(SemanticModel model, SyntaxNode node) => node switch
    {
        IdentifierNameSyntax identifier => model.GetSymbolInfo(identifier).Symbol,
        GenericNameSyntax genericName => model.GetSymbolInfo(genericName).Symbol,
        MemberAccessExpressionSyntax memberAccess => model.GetSymbolInfo(memberAccess).Symbol,
        ObjectCreationExpressionSyntax objectCreation => model.GetSymbolInfo(objectCreation.Type).Symbol,
        _ => null
    };

    private static bool TryGetDiagnostic(ISymbol symbol, out string diagnosticId, out string message)
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
                    diagnosticId = SecurityDiagnosticId;
                    message = bannedType.Message;
                    return true;
                }

                if (IsBlockingMember(symbol, type))
                {
                    diagnosticId = BlockingDiagnosticId;
                    message = "Synchronous waiting is not available to scripts. Use the asynchronous game API instead.";
                    return true;
                }

                if (symbol.Name == "GetResult" &&
                    type.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices" &&
                    type.MetadataName.Contains("Awaiter", StringComparison.Ordinal))
                {
                    diagnosticId = BlockingDiagnosticId;
                    message = "Synchronous waiting through GetAwaiter().GetResult() is not available to scripts. Use await instead.";
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
                    diagnosticId = SecurityDiagnosticId;
                    message = bannedNamespace.Message;
                    return true;
                }
            }
        }

        diagnosticId = string.Empty;
        message = string.Empty;
        return false;
    }

    private static bool IsBlockingMember(ISymbol symbol, INamedTypeSymbol type)
    {
        var originalType = type.OriginalDefinition;
        var typeNamespace = originalType.ContainingNamespace?.ToDisplayString();
        return BlockingMembers.Any(rule =>
            string.Equals(typeNamespace, rule.Namespace, StringComparison.Ordinal) &&
            string.Equals(originalType.MetadataName, rule.TypeMetadataName, StringComparison.Ordinal) &&
            string.Equals(symbol.Name, rule.MemberName, StringComparison.Ordinal));
    }
}
