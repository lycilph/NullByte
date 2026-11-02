using System.Text.RegularExpressions;

namespace NullByte.Scripting;

// ---------------------------------------------------------------------------
// Script libraries: reusable code shared between player scripts.
//
// A script declares dependencies with header lines at the top of its source:
//     // uses: rotation, math
// The host loads scripts/lib/<name>.cs for each, guard-validates it, and
// chains it AHEAD of the script via Roslyn's ContinueWith — everything
// compiles as one unit, so the script can call anything the libraries
// declare (top-level methods, classes, records, ...).
//
// Conventions:
//  - Libraries are pure source with no state of their own: a lib that needs
//    engine access takes `ns` as a parameter (e.g. Plan(ns)) — the calling
//    script's ns.Data stays the single source of durable truth.
//  - Names are restricted to [a-z0-9-]+ — no path tricks.
//  - Chain order = header order; declaration collisions across libs are
//    compile errors (deliberate: no silent shadowing).
//  - TODO (production hardening): cache compiled library chains per content
//    hash — recompiling every lib on every `run` is fine at this scale.
// ---------------------------------------------------------------------------

internal static partial class ScriptIncludes
{
        [GeneratedRegex(@"^\s*//\s*uses:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline, "da-DK")]
    private static partial Regex GeneratedUsesRegex();

    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.IgnoreCase, "da-DK")]
    private static partial Regex GeneratedNameRegex();

    private static readonly Regex UsesRegex = GeneratedUsesRegex();
    private static readonly Regex NameRegex = GeneratedNameRegex();

    /// <summary>All library names the script asks for, in header order.</summary>
    public static List<string> ParseUses(string source)
        => UsesRegex.Matches(source)
            .SelectMany(m => m.Groups[1].Value.Split(',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    /// <summary>Loads and guard-validates one library. Throws on bad names,
    /// missing files, and banned patterns.</summary>
    public static string LoadLibrary(string name)
    {
        if (!NameRegex.IsMatch(name))
            throw new ScriptCompileException(
                $"Invalid library name '{name}' — allowed: letters, digits, dashes.");
        string path = ScriptPersistence.LibPath(name);
        if (!File.Exists(path))
            throw new ScriptCompileException($"Library '{name}' not found at {path}.");
        string source = File.ReadAllText(path);
        ScriptGuard.Validate(source);
        return source;
    }

}