using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Services;
using NullByte.Core.Systems;
using NullByte.Core.Time;
using NullByte.Core.World;

namespace NullByte.Core.Tests.Simulation;

/// <summary>
/// The M4 acceptance scenario: before scripting exists, a player who only
/// hacks by hand and reinvests in the botnet must be able to grow it.
/// </summary>
public sealed class BotnetEarlyGameTests
{
    private static readonly TimeSpan PlayTime = TimeSpan.FromHours(2);
    private const int RequiredNodes = 3;

    [Fact]
    public async Task HandHackingAndBotnetIncome_ReachSeveralNodesWithoutScripts()
    {
        var balance = new BalanceConfig();
        var gameState = InitialGameStateFactory.Create();
        var player = gameState.Player;

        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);
        var hackingFormulas = new HackingFormulas(balance);
        var botnetFormulas = new BotnetFormulas(balance);
        var actions = new ServerActions(gameState, scheduler, hackingFormulas, new SeededRandomSource(balance.RandomSeed));
        var service = new BotnetService(gameState, botnetFormulas);
        var system = new BotnetSystem(gameState, botnetFormulas);

        while (clock.Elapsed < PlayTime)
        {
            var target = ChooseHackTarget(gameState, hackingFormulas);
            var duration = hackingFormulas.CalculateHackDuration(player, target);

            var hack = actions.HackAsync(target.Id);

            // The botnet keeps paying while the player waits for the hack.
            Advance(clock, scheduler, system, duration);
            await hack;

            BuyWhileAffordable(gameState, service, botnetFormulas);
        }

        Assert.True(
            gameState.Botnet.NodeCount >= RequiredNodes,
            $"Expected at least {RequiredNodes} nodes after {PlayTime.TotalMinutes:N0} game minutes " +
            $"of hand hacking, but reached {gameState.Botnet.NodeCount} " +
            $"(producing ${botnetFormulas.CalculateTotalProduction(gameState.Botnet):N2}/s).");

        Assert.True(gameState.Botnet.TotalEarned > 0);
    }

    private static void Advance(FakeGameClock clock, GameScheduler scheduler, BotnetSystem system, TimeSpan duration)
    {
        var step = TimeSpan.FromSeconds(1);
        var remaining = duration;

        while (remaining > TimeSpan.Zero)
        {
            var slice = remaining < step ? remaining : step;

            clock.Advance(slice);
            system.Update(slice);
            remaining -= slice;
        }

        scheduler.Update();
    }

    /// <summary>
    /// Picks the server with the best expected income per second among those
    /// the player can already hack, taking root on any that have become
    /// available, as a player would with nuke.
    /// </summary>
    private static Server ChooseHackTarget(GameState gameState, HackingFormulas formulas)
    {
        var player = gameState.Player;
        var level = formulas.CalculateHackingLevel(player.HackingExperience);

        Server? best = null;
        var bestValue = double.MinValue;

        foreach (var server in gameState.Network.Servers)
        {
            if (server.Id == player.HomeHostname || !server.TryGainRootAccess(level))
                continue;

            var chance = formulas.CalculateHackChance(player, server);
            var amount = formulas.CalculateHackAmount(player, server);
            var seconds = formulas.CalculateHackDuration(player, server).TotalSeconds;
            var value = chance * amount / seconds;

            if (value > bestValue)
            {
                best = server;
                bestValue = value;
            }
        }

        return best ?? throw new InvalidOperationException("No server can be hacked.");
    }

    /// <summary>
    /// Spends money on whichever purchase adds the most production per dollar,
    /// saving up for it when it is not yet affordable.
    /// </summary>
    private static void BuyWhileAffordable(GameState gameState, BotnetService service, BotnetFormulas formulas)
    {
        while (true)
        {
            var best = FindBestPurchase(gameState, service, formulas);

            if (gameState.Player.Money < best.Cost)
                return;

            var result = best.Buy();
            Assert.True(result.Succeeded, result.FailureMessage);
        }
    }

    private static (double Cost, double Ratio, Func<BotnetPurchaseResult> Buy) FindBestPurchase(
        GameState gameState,
        BotnetService service,
        BotnetFormulas formulas)
    {
        var nodeCost = service.GetNextNodeCost();
        var best = (Cost: nodeCost, Ratio: nodeCost / formulas.CalculateProduction(1, 1, 1), Buy: (Func<BotnetPurchaseResult>)service.PurchaseNode);

        for (var index = 0; index < gameState.Botnet.NodeCount; index++)
        {
            var node = gameState.Botnet.Nodes[index];
            var current = formulas.CalculateNodeProduction(node);
            var nodeIndex = index;

            foreach (var kind in Enum.GetValues<BotnetUpgradeKind>())
            {
                var cost = service.GetUpgradeCost(index, kind);

                if (cost is null)
                    continue;

                var upgraded = kind switch
                {
                    BotnetUpgradeKind.Level => formulas.CalculateProduction(node.Level + 1, node.Ram, node.Cores),
                    BotnetUpgradeKind.Ram => formulas.CalculateProduction(node.Level, node.Ram * 2, node.Cores),
                    _ => formulas.CalculateProduction(node.Level, node.Ram, node.Cores + 1)
                };

                var ratio = cost.Value / (upgraded - current);

                if (ratio < best.Ratio)
                {
                    var upgradeKind = kind;
                    best = (cost.Value, ratio, () => service.Upgrade(nodeIndex, upgradeKind));
                }
            }
        }

        return best;
    }
}