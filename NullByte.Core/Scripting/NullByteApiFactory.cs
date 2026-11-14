using NullByte.Core.Domain;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.Core.Scripting;

public sealed class NullByteApiFactory(
    GameState gameState,
    GameScheduler scheduler,
    ServerActions serverActions,
    IGameOutput output) : INullByteApiFactory
{
    public INullByteApi Create(int threads, CancellationToken cancellationToken)
    {
        return new NullByteApi(
            gameState,
            scheduler,
            serverActions,
            output,
            threads,
            cancellationToken);
    }
}