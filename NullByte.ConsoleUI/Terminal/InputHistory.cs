namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Remembers previously submitted input lines and lets the user walk back
/// and forth through them, shell style.
/// </summary>
/// <remarks>
/// Not thread-safe. Owned and used by the terminal UI loop only.
/// </remarks>
public sealed class InputHistory
{
    private readonly List<string> entries = [];
    private readonly int capacity;

    // entries.Count means "not browsing".
    private int position;
    private string draft = string.Empty;

    public InputHistory(int capacity = 200)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least one.");

        this.capacity = capacity;
    }

    public int Count => entries.Count;

    public bool IsBrowsing => position < entries.Count;

    /// <summary>
    /// Records a submitted line. Blank lines and immediate repeats are ignored.
    /// Always ends any browsing session.
    /// </summary>
    public void Add(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!string.IsNullOrWhiteSpace(entry) &&
            (entries.Count == 0 || !string.Equals(entries[^1], entry, StringComparison.Ordinal)))
        {
            entries.Add(entry);

            if (entries.Count > capacity)
                entries.RemoveAt(0);
        }

        ResetNavigation();
    }

    /// <summary>
    /// Moves to the next older entry. The first call stashes
    /// <paramref name="currentInput"/> so that <see cref="Next"/> can restore it.
    /// Returns null when there is nothing older to show.
    /// </summary>
    public string? Previous(string currentInput)
    {
        ArgumentNullException.ThrowIfNull(currentInput);

        if (entries.Count == 0 || position == 0)
            return null;

        if (!IsBrowsing)
            draft = currentInput;

        position--;
        return entries[position];
    }

    /// <summary>
    /// Moves to the next newer entry, or back to the stashed draft after the
    /// newest entry. Returns null when not browsing.
    /// </summary>
    public string? Next()
    {
        if (!IsBrowsing)
            return null;

        position++;
        return IsBrowsing ? entries[position] : draft;
    }

    /// <summary>
    /// Leaves browsing mode, keeping whatever is currently typed as the new draft.
    /// </summary>
    public void ResetNavigation()
    {
        position = entries.Count;
        draft = string.Empty;
    }
}