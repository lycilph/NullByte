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
    ServerActions serverActions,
    ActivityService activityService,
    HackingFormulas hackingFormulas,
    BotnetFormulas botnetFormulas,
    SkillFormulas skillFormulas,
    IGameOutput output) : INullByteApiFactory
{
    public INullByteApi Create(IScriptOperations scriptOperations, string processHostname, int threads, CancellationToken cancellationToken)
    {
        return new NullByteApi(
            gameState,
            scheduler,
            networkService,
            botnetService,
            serverActions,
            activityService,
            hackingFormulas,
            botnetFormulas,
            skillFormulas,
            scriptOperations,
            output,
            processHostname,
            threads,
            cancellationToken);
    }
}