using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NullByte.Scripting.Compilation;

/// <summary>
/// Performs semantic checks for APIs that player scripts must not access.
/// The checks are deliberately based on resolved Roslyn symbols rather than source text.
/// This is a set of practical guardrails, not a hardened security boundary (see ARCHITECTURE.md).
/// </summary>
internal static class ScriptSecurityAnalyzer
{
    private const string SecurityDiagnosticId = "NB1007";
    private const string BlockingDiagnosticId = "NB1008";
    private const string CooperativeLoopDiagnosticId = "NB1009";

    private const string InternalsMessage = "Game internals are not available to scripts. Use the nb API instead.";
    private const string BackgroundWorkMessage = "Starting background work is not available to scripts. Use the asynchronous game API instead.";
    private const string ReflectionMessage = "Reflection is not available to scripts.";

    private static readonly (string Namespace, string Message)[] BannedNamespaces =
    [
        ("System.IO", "File system access is not available to scripts."),
        ("System.Net", "Network access is not available to scripts."),
        ("System.Reflection", ReflectionMessage),
        ("System.Runtime.InteropServices", "Native interop is not available to scripts."),
        ("System.Runtime.Loader", "Loading assemblies is not available to scripts."),
        ("NullByte.Core.Domain", InternalsMessage),
        ("NullByte.Core.Persistence", InternalsMessage),
        ("NullByte.Core.Simulation", InternalsMessage),
        ("NullByte.Core.Systems", InternalsMessage),
        ("NullByte.Core.Threading", InternalsMessage),
        ("NullByte.Core.Time", InternalsMessage),
        ("NullByte.Core.World", InternalsMessage),
        ("NullByte.Core.Formulas", InternalsMessage),
        ("NullByte.Core.Randomness", InternalsMessage),
        ("NullByte.Core.Configuration", InternalsMessage)
    ];

    // Compared against the original (unconstructed) definition of the referenced type.
    private static readonly (string TypeName, string Message)[] BannedTypes =
    [
        ("System.Diagnostics.Process", "Starting or controlling host processes is not available to scripts."),
        ("System.Diagnostics.Debugger", "Attaching to or controlling the debugger is not available to scripts."),
        ("System.Environment", "Access to the host environment is not available to scripts."),
        ("System.AppDomain", "Access to the host application domain is not available to scripts."),
        ("System.GC", "Controlling the host garbage collector is not available to scripts."),
        ("System.Activator", ReflectionMessage),
        ("System.Runtime.CompilerServices.Unsafe", "Unsafe memory access is not available to scripts."),
        ("System.Threading.Thread", "Creating or controlling threads is not available to scripts."),
        ("System.Threading.ThreadPool", "Access to the host thread pool is not available to scripts."),
        ("System.Threading.WaitHandle", "Direct synchronization with host wait handles is not available to scripts."),
        ("System.Threading.Timer", BackgroundWorkMessage),
        ("System.Threading.PeriodicTimer", BackgroundWorkMessage),
        ("System.Threading.Tasks.Parallel", BackgroundWorkMessage),
        ("System.Threading.Tasks.TaskFactory", BackgroundWorkMessage),
        ("System.Threading.Tasks.TaskFactory<TResult>", BackgroundWorkMessage),
        ("System.Threading.Tasks.TaskScheduler", BackgroundWorkMessage),
        // The concrete API implementations and host services behind nb: scripts only get the interfaces.
        ("NullByte.Core.Scripting.NullByteApi", InternalsMessage),
        ("NullByte.Core.Scripting.NullByteApiFactory", InternalsMessage),
        ("NullByte.Core.Scripting.NullByteFilesApi", InternalsMessage),
        ("NullByte.Core.Scripting.NullByteBotnetApi", InternalsMessage),
        ("NullByte.Core.Scripting.NullByteActivitiesApi", InternalsMessage),
        ("NullByte.Core.Services.ServerActions", InternalsMessage),
        ("NullByte.Core.Services.NetworkService", InternalsMessage),
        ("NullByte.Core.Services.BotnetService", InternalsMessage),
        ("NullByte.Core.Services.ActivityService", InternalsMessage),
        ("NullByte.Core.Services.WorkingDirectory", InternalsMessage),
        ("NullByte.Core.Services.IGameOutput", InternalsMessage)
    ];

    // Individual members of otherwise harmless types that hand out reflection or background execution.
    private static readonly (string Namespace, string TypeMetadataName, string[] MemberNames, string Message)[] BannedMembers =
    [
        ("System", "Type", new[]
        {
            "GetType", "InvokeMember", "Assembly", "Module",
            "GetMethod", "GetMethods", "GetField", "GetFields", "GetProperty", "GetProperties",
            "GetConstructor", "GetConstructors", "GetMember", "GetMembers", "GetEvent", "GetEvents",
            "GetNestedType", "GetNestedTypes"
        }, ReflectionMessage),
        ("System", "Delegate", new[] { "CreateDelegate", "DynamicInvoke" }, ReflectionMessage),
        ("System.Threading.Tasks", "Task", new[] { "Run", "Factory", "Start", "RunSynchronously", "ContinueWith" }, BackgroundWorkMessage),
        ("System.Threading.Tasks", "Task`1", new[] { "Factory", "ContinueWith" }, BackgroundWorkMessage)
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
            var root = tree.GetRoot();

            foreach (var loop in root.DescendantNodes().OfType<StatementSyntax>().Where(IsLoopStatement))
            {
                if (!CanRunWithoutEnding(loop, model) || YieldsToHost(loop, model))
                    continue;

                yield return CreateDiagnostic(
                    tree,
                    loop.GetLocation(),
                    CooperativeLoopDiagnosticId,
                    "This loop can run without ever yielding to the game. Await an asynchronous operation inside it " +
                    "(for example await nb.Sleep(...)), or use a for or foreach loop over a finite range.");
            }

            // Nodes arrive outermost first, so a nested reference that is already covered by a reported
            // expression (System.IO.File.ReadAllText, new Thread(...)) does not produce a second error.
            var reported = new List<(TextSpan Span, string Id)>();

            foreach (var node in root.DescendantNodes())
            {
                var symbol = GetReferencedSymbol(model, node);
                if (symbol is null || !TryGetDiagnostic(symbol, out var diagnosticId, out var message))
                    continue;

                if (reported.Any(entry => entry.Id == diagnosticId && entry.Span.Contains(node.Span)))
                    continue;

                reported.Add((node.Span, diagnosticId));
                yield return CreateDiagnostic(tree, node.GetLocation(), diagnosticId, message);
            }
        }
    }

    private static ScriptDiagnostic CreateDiagnostic(SyntaxTree tree, Location location, string id, string message)
    {
        var span = location.GetMappedLineSpan();
        return new ScriptDiagnostic(
            id,
            message,
            ScriptDiagnosticSeverity.Error,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            tree.FilePath,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    private static bool IsLoopStatement(StatementSyntax statement) => statement is
        WhileStatementSyntax or
        DoStatementSyntax or
        ForStatementSyntax or
        ForEachStatementSyntax or
        ForEachVariableStatementSyntax;

    /// <summary>
    /// Whether the loop has no built-in end. A <c>foreach</c> or a <c>for</c> with a condition walks a
    /// finite range in ordinary code, so only <c>while</c>, <c>do</c> and condition-less or constant-true
    /// <c>for</c> loops need to yield. This is a syntactic judgement, not a termination proof.
    /// </summary>
    private static bool CanRunWithoutEnding(StatementSyntax loop, SemanticModel model) => loop switch
    {
        WhileStatementSyntax or DoStatementSyntax => true,
        ForStatementSyntax { Condition: null } => true,
        ForStatementSyntax { Condition: { } condition } => model.GetConstantValue(condition).Value is true,
        _ => false
    };

    private static bool YieldsToHost(StatementSyntax loop, SemanticModel model) =>
        loop.DescendantNodes(descendIntoChildren: node =>
                node == loop ||
                node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .Any(node => IsYieldPoint(node, model));

    private static bool IsYieldPoint(SyntaxNode node, SemanticModel model) => node switch
    {
        // Awaiting something that is already complete continues synchronously, so it does not yield.
        AwaitExpressionSyntax awaitExpression => !IsCompletedTask(awaitExpression.Expression, model),
        CommonForEachStatementSyntax forEach => forEach.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword),
        UsingStatementSyntax usingStatement => usingStatement.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword),
        LocalDeclarationStatementSyntax declaration => declaration.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword),
        _ => false
    };

    private static bool IsCompletedTask(ExpressionSyntax expression, SemanticModel model)
    {
        var target = expression is InvocationExpressionSyntax invocation ? invocation.Expression : expression;
        var symbol = model.GetSymbolInfo(target).Symbol;

        return symbol is { Name: "CompletedTask" or "FromResult" or "FromCanceled" or "FromException" }
            && symbol.ContainingType is { MetadataName: "Task" or "ValueTask" } type
            && type.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks";
    }

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
                var typeName = type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty, StringComparison.Ordinal);

                var bannedType = BannedTypes.FirstOrDefault(rule =>
                    string.Equals(typeName, rule.TypeName, StringComparison.Ordinal));
                if (bannedType != default)
                {
                    diagnosticId = SecurityDiagnosticId;
                    message = bannedType.Message;
                    return true;
                }

                if (TryGetBannedMember(symbol, type, out message))
                {
                    diagnosticId = SecurityDiagnosticId;
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

    private static bool TryGetBannedMember(ISymbol symbol, INamedTypeSymbol type, out string message)
    {
        var originalType = type.OriginalDefinition;
        var typeNamespace = originalType.ContainingNamespace?.ToDisplayString();

        foreach (var rule in BannedMembers)
        {
            if (string.Equals(typeNamespace, rule.Namespace, StringComparison.Ordinal) &&
                string.Equals(originalType.MetadataName, rule.TypeMetadataName, StringComparison.Ordinal) &&
                rule.MemberNames.Contains(symbol.Name, StringComparer.Ordinal))
            {
                message = rule.Message;
                return true;
            }
        }

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
