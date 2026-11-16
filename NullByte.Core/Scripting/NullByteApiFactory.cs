using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.Core.Scripting;

public sealed class NullByteApiFactory(
    GameState gameState,
    GameScheduler scheduler,
    NetworkService networkService,
    ServerActions serverActions,
    HackingFormulas hackingFormulas,
    IGameOutput output) : INullByteApiFactory
{
    public INullByteApi Create(IScriptOperations scriptOperations, string processHostname, int threads, CancellationToken cancellationToken)
    {
        return new NullByteApi(
            gameState,
            scheduler,
            networkService,
            serverActions,
            hackingFormulas,
            scriptOperations,
            output,
            processHostname,
            threads,
            cancellationToken);
    }
}