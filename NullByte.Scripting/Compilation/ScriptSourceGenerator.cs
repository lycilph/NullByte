using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace NullByte.Scripting.Compilation;

/// <summary>
/// Wraps a player's top-level script in a generated <c>IScript</c> class.
/// </summary>
/// <remarks>
/// The player writes usings, then statements, then (optionally) type
/// declarations. Usings stay where they are, the statements become the body of
/// <c>Run(nb, args)</c>, and the type declarations stay outside the class.
/// <c>#line</c> directives map every diagnostic back to the player's own file.
/// </remarks>
internal static class ScriptSourceGenerator
{
    /// <summary>Name of the generated class; the runner looks it up by this name.</summary>
    public const string TypeName = "__NullByteScript";

    public static string Generate(SourceText source, CompilationUnitSyntax root, string fileName)
    {
        // Everything before the first statement: extern aliases, usings, assembly attributes.
        var headerEnd = new[]
        {
            root.Externs.Count > 0 ? root.Externs[^1].FullSpan.End : 0,
            root.Usings.Count > 0 ? root.Usings[^1].FullSpan.End : 0,
            root.AttributeLists.Count > 0 ? root.AttributeLists[^1].FullSpan.End : 0
        }.Max();

        // Top-level rules: statements come first, type declarations after them.
        var firstDeclaration = root.Members.FirstOrDefault(member => member is not GlobalStatementSyntax);
        var bodyEnd = firstDeclaration?.FullSpan.Start ?? source.Length;

        var header = source.ToString(TextSpan.FromBounds(0, headerEnd));
        var body = source.ToString(TextSpan.FromBounds(headerEnd, bodyEnd));
        var declarations = source.ToString(TextSpan.FromBounds(bodyEnd, source.Length));

        var bodyLine = LineOf(source, headerEnd);
        var declarationsLine = LineOf(source, bodyEnd);
        var file = EscapeFileName(fileName);

        var builder = new StringBuilder();

        AppendLine(builder, header);
        builder.AppendLine("#pragma warning disable CS1998 // scripts do not have to await anything");
        builder.AppendLine("#line hidden");
        builder.AppendLine($"public sealed class {TypeName} : global::NullByte.Core.Scripting.IScript");
        builder.AppendLine("{");
        builder.AppendLine("    public async global::System.Threading.Tasks.Task Run(");
        builder.AppendLine("        global::NullByte.Core.Scripting.INullByteApi nb, string[] args)");
        builder.AppendLine("    {");
        builder.AppendLine($"#line {bodyLine} \"{file}\"");
        AppendLine(builder, body);
        builder.AppendLine("#line hidden");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("#pragma warning restore CS1998");

        if (!string.IsNullOrWhiteSpace(declarations))
        {
            builder.AppendLine($"#line {declarationsLine} \"{file}\"");
            AppendLine(builder, declarations);
        }

        return builder.ToString();
    }

    private static int LineOf(SourceText source, int position)
        => source.Lines.GetLineFromPosition(Math.Min(position, source.Length)).LineNumber + 1;

    // Make sure whatever follows (a directive, a brace) starts on its own line.
    private static void AppendLine(StringBuilder builder, string text)
    {
        builder.Append(text);

        if (text.Length == 0 || text[^1] is not ('\n' or '\r'))
            builder.AppendLine();
    }

    private static string EscapeFileName(string fileName)
        => fileName.Replace("\\", "\\\\", StringComparison.Ordinal)
                   .Replace("\"", "\\\"", StringComparison.Ordinal);
}