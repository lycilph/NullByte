using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NullByte.Scripting.Compilation;

internal sealed record ParsedIncludes(string Source,IReadOnlyList<string> Includes, IReadOnlyList<ScriptDiagnostic> Diagnostics);

internal static class ScriptIncludeParser
{
    private const string IncludePrefix = "#include";
    private const string InvalidIncludeDiagnosticId = "NB0001";
    private const string TopLevelInIncludeDiagnosticId = "NB0002";

    public static ParsedIncludes ParseDirectives(string source, string fileName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var text = SourceText.From(source);
        var includes = new List<string>();
        var diagnostics = new List<ScriptDiagnostic>();
        var outputLines = new string[text.Lines.Count];

        for (var i = 0; i < text.Lines.Count; i++)
        {
            var line = text.Lines[i];
            var rawLine = line.ToString();
            var trimmedStart = rawLine.TrimStart();

            if (!trimmedStart.StartsWith(IncludePrefix, StringComparison.Ordinal))
            {
                outputLines[i] = rawLine;
                continue;
            }

            // Ensure '#include' is a full directive keyword, not '#includedFoo'.
            var afterKeywordIndex = rawLine.IndexOf(IncludePrefix, StringComparison.Ordinal) + IncludePrefix.Length;
            if (afterKeywordIndex < rawLine.Length && !char.IsWhiteSpace(rawLine[afterKeywordIndex]))
            {
                outputLines[i] = rawLine;
                continue;
            }

            var argument = rawLine[afterKeywordIndex..].Trim();
            if (string.IsNullOrWhiteSpace(argument))
            {
                diagnostics.Add(new ScriptDiagnostic(
                    InvalidIncludeDiagnosticId,
                    "Expected a filename after '#include'.",
                    ScriptDiagnosticSeverity.Error,
                    Line: i + 1,
                    Column: rawLine.Length + 1));
            }
            else
            {
                includes.Add(argument);
            }

            // Blank out the directive line so line numbers remain 1:1 with the original file.
            outputLines[i] = string.Empty;
        }

        var lineEnding = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return new ParsedIncludes(string.Join(lineEnding, outputLines), includes, diagnostics);
    }

    public static IReadOnlyList<ScriptDiagnostic> ValidateIncludedScript(CompilationUnitSyntax compilationUnit, string fileName)
    {
        ArgumentNullException.ThrowIfNull(compilationUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var diagnostics = new List<ScriptDiagnostic>();

        foreach (var member in compilationUnit.Members)
        {
            if (member is not GlobalStatementSyntax globalStatement)
                continue;

            var lineSpan = globalStatement.GetLocation().GetLineSpan();
            diagnostics.Add(new ScriptDiagnostic(
                TopLevelInIncludeDiagnosticId,
                $"Included script '{fileName}' may only contain usings and type/member declarations, not top-level statements.",
                ScriptDiagnosticSeverity.Error,
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1));
        }

        return diagnostics;
    }
}