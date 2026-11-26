using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

/// <summary>
/// A save store that keeps its slots in memory, so tests never touch the file system.
/// </summary>
internal sealed class InMemorySaveStore : ISaveStore
{
    private readonly Dictionary<string, string> slots = [];

    /// <summary>Makes <see cref="DeleteAsync"/> fail, for tests of the failure paths.</summary>
    public bool FailDelete { get; init; }

    public Task WriteAsync(string slot, string json, CancellationToken cancellationToken = default)
    {
        lock (slots)
            slots[slot] = json;

        return Task.CompletedTask;
    }

    public Task<string?> ReadAsync(string slot, CancellationToken cancellationToken = default)
    {
        lock (slots)
            return Task.FromResult(slots.GetValueOrDefault(slot));
    }

    public Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (slots)
            return Task.FromResult<IReadOnlyList<SaveSlotInfo>>(
                slots.Keys.Select(name => new SaveSlotInfo(name, DateTimeOffset.UnixEpoch)).ToArray());
    }

    public Task DeleteAsync(string slot, CancellationToken cancellationToken = default)
    {
        if (FailDelete)
            throw new IOException("The test store rejected deletion.");

        lock (slots)
            slots.Remove(slot);

        return Task.CompletedTask;
    }
}
