namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Flavor: random terminal lines from content JSON. Pure presentation —
// never affects gameplay numbers, so engine randomness stays harmless.
// ---------------------------------------------------------------------------

public static class Flavor
{
    private static readonly Random rng = new();

    public static string Pick(IReadOnlyList<string> lines)
        => lines.Count == 0 ? "" : lines[rng.Next(lines.Count)];

    public static string Fill(string template,
                              string target = "",
                              double money = 0,
                              double xp = 0)
        => template
            .Replace("{target}", target)
            .Replace("{money}", money.ToString("0.##"))
            .Replace("{xp}", xp.ToString("0.#"));
}