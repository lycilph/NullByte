namespace NullByte.Core.Narrative;

/// <summary>
/// A host-neutral narrative message emitted after a durable progression change.
/// </summary>
public sealed record NarrativeMessage(string Id, string Text);
