using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NullByte.Core.Scripting;

namespace NullByte.Scripting.Compilation;

internal static class ScriptRamAnalyzer
{
    private const double BaseCost = 1.6;

    /// <summary>
    /// Base cost plus the cost of every distinct API method called anywhere in the given trees
    /// (the generated script and all included files). Included files count in full, whether or
    /// not the script calls the functions in them.
    /// </summary>
    public static double Calculate(CSharpCompilation compilation, IEnumerable<SyntaxTree> trees)
    {
        var ramCostAttribute = compilation.GetTypeByMetadataName(typeof(RamCostAttribute).FullName!);
        var methods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in trees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);

            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol symbol)
                    continue;

                if (symbol.GetAttributes().Any(attribute =>
                        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, ramCostAttribute)))
                    methods.Add(symbol);
            }
        }

        return BaseCost + methods.Sum(GetRamCost);
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