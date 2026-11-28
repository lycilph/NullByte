namespace NullByte.Core.Narrative;

/// <summary>
/// A single authored narrative entry associated with a durable gameplay milestone.
/// Placeholders use the form {name} and are supplied by the coordinator at delivery time.
/// </summary>
public sealed record NarrativeEntry(string Id, NarrativeAct Act, string MilestoneId, string Text)
{
    public string Render(IReadOnlyDictionary<string, string>? values = null)
    {
        var rendered = Text;
        if (values is null)
            return rendered;

        foreach (var (key, value) in values)
            rendered = rendered.Replace("{" + key + "}", value, StringComparison.Ordinal);

        return rendered;
    }
}
