using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.Core.Scripting;

public sealed class NullByteApiFactory(
    GameState gameState,
    GameScheduler scheduler,
    NetworkService networkService,
    BotnetService botnetService,
    CloudServerService cloudServerService,
    ServerActions serverActions,
    ActivityService activityService,
    FormulaSet formulas,
    IGameOutput output) : INullByteApiFactory
{
    public INullByteApi Create(IScriptOperations scriptOperations, ServerId processHostServerId, int threads, CancellationToken cancellationToken)
    {
        return new NullByteApi(
            gameState,
            scheduler,
            networkService,
            botnetService,
            cloudServerService,
            serverActions,
            activityService,
            formulas.Skills,
            formulas.Botnet,
            scriptOperations,
            output,
            processHostServerId,
            threads,
            cancellationToken);
    }
}