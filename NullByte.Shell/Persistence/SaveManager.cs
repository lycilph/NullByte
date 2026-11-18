using NullByte.Core.Persistence;

namespace NullByte.Shell.Persistence;

public sealed class SaveManager
{
    private readonly ISaveStore store;
    private readonly SaveGameSerializer serializer;
    private readonly TimeProvider time;
    private readonly SemaphoreSlim gate = new(1, 1);

    public SaveManager(ISaveStore store, SaveGameSerializer serializer, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(serializer);

        this.store = store;
        this.serializer = serializer;
        time = timeProvider ?? TimeProvider.System;
    }

    public async Task SaveAsync(GameSession session, string slot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await session.CaptureSaveAsync(time.GetUtcNow())
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            var json = serializer.Serialize(snapshot);

            await store.WriteAsync(slot, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RestoredGame?> LoadAsync(string slot, CancellationToken cancellationToken = default)
    {
        var json = await store.ReadAsync(slot, cancellationToken).ConfigureAwait(false);

        return json is null
            ? null
            : SaveGameMapper.Restore(serializer.Deserialize(json));
    }

    public Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
        => store.ListAsync(cancellationToken);
}