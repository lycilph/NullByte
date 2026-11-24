using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Text.RegularExpressions;

namespace NullByte.Scripting.Compilation;

/// <summary>One <c>#include "path"</c> line: the path as typed, and where the directive sits in the file.</summary>
internal sealed record IncludeDirective(string Path, int Start, int Length, int Line, int Column, int EndLine, int EndColumn);

internal static class IncludeScanner
{
    private static readonly Regex StartsWithInclude = new(@"^#\s*include\b", RegexOptions.Compiled);
    private static readonly Regex WellFormed = new(@"^#\s*include\s+""(?<path>[^""\r\n]*)""\s*(//.*)?$", RegexOptions.Compiled);

    public static IReadOnlyList<IncludeDirective> Scan(string source, string fileName, List<ScriptDiagnostic> diagnostics)
    {
        var text = SourceText.From(source, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(text, ScriptCompiler.ParseOptions);
        var result = new List<IncludeDirective>();

        // Roslyn does not know #include, so it shows up as a bad directive. Strings and comments never do.
        foreach (var trivia in tree.GetRoot().DescendantTrivia().Where(t => t.IsKind(SyntaxKind.BadDirectiveTrivia)))
        {
            var directive = trivia.ToString().Trim();
            if (!StartsWithInclude.IsMatch(directive))
                continue;   // some other bad directive: the normal syntax errors report it

            var position = text.Lines.GetLinePosition(trivia.Span.Start);
            var endPosition = text.Lines.GetLinePosition(trivia.Span.End);
            var match = WellFormed.Match(directive);

            if (!match.Success)
            {
                diagnostics.Add(new ScriptDiagnostic("NB1005",
                    "Malformed #include. Expected: #include \"path/to/file.cx\"",
                    ScriptDiagnosticSeverity.Error, position.Line + 1, position.Character + 1, fileName,
                    endPosition.Line + 1, endPosition.Character + 1));
                continue;
            }

            result.Add(new IncludeDirective(match.Groups["path"].Value,
                trivia.Span.Start, trivia.Span.Length, position.Line + 1, position.Character + 1,
                endPosition.Line + 1, endPosition.Character + 1));
        }

        return result;
    }

    public static string Blank(string source, IReadOnlyList<IncludeDirective> directives)
    {
        if (directives.Count == 0)
            return source;

        var chars = source.ToCharArray();
        foreach (var directive in directives)
            for (var i = directive.Start; i < directive.Start + directive.Length; i++)
                if (chars[i] is not ('\r' or '\n'))
                    chars[i] = ' ';

        return new string(chars);
    }
}