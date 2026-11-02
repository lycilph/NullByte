using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace NullByte.Scripting;

// ---------------------------------------------------------------------------
// ScriptGuard: compile-time lockdown screen, run BEFORE a script compiles.
//
// Reality check (learned the hard way): WithUsings is NOT a security boundary.
// It only controls default imports — a script's own 'using' directives are
// legal syntax, and Roslyn's default references include the runtime (on
// .NET Core, System.IO.File is even type-forwarded inside System.Runtime.dll,
// so reference trimming can't remove it either).
//
// So we screen the syntax tree directly:
//   1. '#r' directives rejected — Roslyn scripting honors them to LOAD
//      arbitrary assemblies (the biggest hole).
//   2. 'using' directives must be in the whitelist (clarity: host controls
//      imports, scripts don't).
//   3. Banned identifiers rejected anywhere they appear — catches using
//      aliases, fully-qualified access (System.IO.File), and helper types.
//
// Honest limits: an identifier blacklist is defense against accidents and
// casual abuse, not a determined attacker — that needs reference pinning +
// a real DiagnosticAnalyzer (design-doc TODO). Fine for a local single-player
// game; revisit before scripts are ever shared.
// ---------------------------------------------------------------------------

internal static class ScriptGuard
{
    private static readonly HashSet<string> allowedUsings =
    [
        "System", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks",
        "NullByte.Scripting.ScriptingApi"
    ];

    private static readonly HashSet<string> bannedIdentifiers = new(StringComparer.Ordinal)
    {
        // File system & OS surface
        "File", "Directory", "Path", "DriveInfo", "FileSystemInfo", "FileStream",
        "StreamReader", "StreamWriter", "BinaryReader", "BinaryWriter",
        "Console", "Environment",
        // Process / native / reflection escapes
        "Process", "ProcessStartInfo", "AppDomain", "Assembly", "Activator",
        "Marshal", "GCHandle", "DynamicMethod", "MethodInfo", "FieldInfo", "PropertyInfo",
        // Threading escapes (scripts use ns.Wait, not real time)
        "Thread", "ThreadPool", "Mutex", "Semaphore", "SpinWait",
        // Network
        "HttpClient", "WebClient", "WebRequest", "HttpWebRequest", "Socket",
        "TcpClient", "UdpClient", "Dns", "NetworkInterface", "IPAddress", "IPEndPoint",
        // Registry / string-compiled-code escapes
        "Registry", "RegistryKey", "Expression", "LambdaExpression",
    };

    public static void Validate(string source)
    {
        // 1) '#r' would load arbitrary assemblies — hard reject, before parsing.
        if (Regex.IsMatch(source, @"^\s*#\s*r\b", RegexOptions.Multiline))
            throw new ScriptCompileException(
                "BANNED: '#r' directives are not allowed — script references are host-controlled.");

        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

        // 2) Own using directives must be whitelisted.
        foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            string name = u.Name?.ToString() ?? "";
            if (!allowedUsings.Contains(name))
                throw new ScriptCompileException(
                    $"BANNED: using '{name}' is not whitelisted. Allowed: {string.Join(", ", allowedUsings)}");
        }

        // 3) Banned identifiers, wherever they appear (aliases, qualified names, all of it).
        foreach (var id in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (bannedIdentifiers.Contains(id.Identifier.ValueText))
            {
                int line = id.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                throw new ScriptCompileException(
                    $"BANNED: '{id.Identifier.ValueText}' (line {line}) — " +
                    "scripts have no file/network/console/OS access.");
            }
        }
    }
}