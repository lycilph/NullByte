namespace NullByte.Core.Domain.Progression;

/// <summary>
/// The story as an ordered list of chapters. Chapters are completed strictly in order, one at a time,
/// so the player only ever has one current goal.
/// </summary>
public sealed class Journey
{
    private readonly Dictionary<string, Chapter> byId;

    public Journey(IEnumerable<Chapter> chapters)
    {
        ArgumentNullException.ThrowIfNull(chapters);

        Chapters = chapters.ToArray();
        byId = new Dictionary<string, Chapter>(StringComparer.Ordinal);

        foreach (var chapter in Chapters)
        {
            if (!byId.TryAdd(chapter.Id, chapter))
                throw new ArgumentException($"Chapter id '{chapter.Id}' is used more than once.", nameof(chapters));
        }
    }

    public IReadOnlyList<Chapter> Chapters { get; }

    public bool TryGet(string id, out Chapter? chapter) => byId.TryGetValue(id, out chapter);

    /// <summary>The first chapter the player has not completed yet, or null when the story is told.</summary>
    public Chapter? NextChapter(ProgressionState progression)
    {
        ArgumentNullException.ThrowIfNull(progression);

        return Chapters.FirstOrDefault(chapter => !progression.HasCompleted(chapter.Id));
    }
}
