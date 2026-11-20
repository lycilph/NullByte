using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NullByte.Core.Scripting;

namespace NullByte.Scripting.Compilation;

internal static class ScriptRamAnalyzer
{
    public static double Calculate(SemanticModel semanticModel, SyntaxNode root)
    {
        var methods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (symbol is null)
                continue;

            var ramCost = symbol
                .GetAttributes()
                .FirstOrDefault(attribute =>
                    SymbolEqualityComparer.Default.Equals(
                        attribute.AttributeClass,
                        semanticModel.Compilation
                            .GetTypeByMetadataName(
                                typeof(RamCostAttribute).FullName!)));

            if (ramCost is null)
                continue;

            methods.Add(symbol);
        }

        return 1.6 + methods.Sum(GetRamCost);
    }

    public static double Calculate(IEnumerable<(SemanticModel Model, SyntaxNode Root)> trees)
    {
        ArgumentNullException.ThrowIfNull(trees);

        var methods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        foreach (var (semanticModel, root) in trees)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol is null)
                    continue;

                var ramCostAttr = symbol
                    .GetAttributes()
                    .FirstOrDefault(attribute =>
                        SymbolEqualityComparer.Default.Equals(
                            attribute.AttributeClass,
                            semanticModel.Compilation
                                .GetTypeByMetadataName(
                                    typeof(RamCostAttribute).FullName!)));

                if (ramCostAttr is not null)
                    methods.Add(symbol);
            }
        }

        return 1.6 + methods.Sum(GetRamCost);
    }

    private static double GetRamCost(IMethodSymbol method)
    {
        var attribute = method.GetAttributes()
            .First(attribute =>
                attribute.AttributeClass?.ToDisplayString() ==
                typeof(RamCostAttribute).FullName);

        if (attribute.ConstructorArguments.Length != 1)
            throw new InvalidOperationException($"Invalid RamCost attribute on {method.ToDisplayString()}.");

        var value = attribute.ConstructorArguments[0].Value;

        if (value is not double cost || !double.IsFinite(cost) || cost < 0)
            throw new InvalidOperationException($"Invalid RAM cost on {method.ToDisplayString()}.");

        return cost;
    }
}