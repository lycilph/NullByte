using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Systems;

namespace NullByte.Core.Tests.Systems;

public sealed class BotnetSystemTests
{
    [Fact]
    public void Update_WithNoNodes_PaysNothing()
    {
        var (state, _, system, _) = Create();

        system.Update(TimeSpan.FromSeconds(60));

        Assert.Equal(0.0, state.Player.Money);
        Assert.Equal(0.0, state.Botnet.TotalEarned);
    }

    [Fact]
    public void Update_PaysProductionTimesElapsedSeconds()
    {
        var (state, _, system, _) = Create();
        state.Botnet.AddNode();

        system.Update(TimeSpan.FromSeconds(10));

        // One level-one node makes 1.5 per second.
        Assert.Equal(15.0, state.Player.Money, 9);
        Assert.Equal(15.0, state.Botnet.TotalEarned, 9);
    }

    [Fact]
    public void Update_ProductionMatchesTheFormulaOverSimulatedTime()
    {
        var (state, service, system, formulas) = Create(startingMoney: 1_000_000);

        for (var i = 0; i < 3; i++)
            service.PurchaseNode();

        service.Upgrade(0, BotnetUpgradeKind.Level);
        service.Upgrade(1, BotnetUpgradeKind.Ram);
        service.Upgrade(1, BotnetUpgradeKind.Ram);
        service.Upgrade(2, BotnetUpgradeKind.Cores);

        var moneyBefore = state.Player.Money;
        var rate = formulas.CalculateTotalProduction(state.Botnet);

        // One simulated minute in one millisecond slices, like a busy game loop.
        for (var i = 0; i < 60_000; i++)
            system.Update(TimeSpan.FromMilliseconds(1));

        var expected = rate * 60.0;

        // TotalEarned only holds botnet income, so it is the precise check. The money
        // balance is a large number, which is coarser to add small amounts to.
        Assert.InRange(state.Botnet.TotalEarned, expected - 1e-6, expected + 1e-6);
        Assert.InRange(state.Player.Money - moneyBefore, expected - 1e-4, expected + 1e-4);
    }

    [Fact]
    public void Update_SliceSizeDoesNotChangeTheTotal()
    {
        var (coarseState, _, coarse, _) = Create();
        var (fineState, _, fine, _) = Create();
        coarseState.Botnet.AddNode();
        fineState.Botnet.AddNode();

        coarse.Update(TimeSpan.FromSeconds(30));
        for (var i = 0; i < 3_000; i++)
            fine.Update(TimeSpan.FromMilliseconds(10));

        Assert.Equal(coarseState.Player.Money, fineState.Player.Money, 6);
    }

    [Fact]
    public void Update_AnUpgradeChangesTheRateFromThenOn()
    {
        var (state, service, system, formulas) = Create(startingMoney: 10_000);
        state.Botnet.AddNode();

        var before = formulas.CalculateTotalProduction(state.Botnet);
        system.Update(TimeSpan.FromSeconds(10));

        var moneyAfterFirstPeriod = state.Player.Money;
        Assert.True(service.Upgrade(0, BotnetUpgradeKind.Level).Succeeded);
        var moneyAfterUpgrade = state.Player.Money;

        var after = formulas.CalculateTotalProduction(state.Botnet);
        system.Update(TimeSpan.FromSeconds(10));

        Assert.True(after > before);
        Assert.Equal(10_000 + before * 10, moneyAfterFirstPeriod, 9);
        Assert.Equal(moneyAfterUpgrade + after * 10, state.Player.Money, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Update_WithNoElapsedTime_DoesNothing(int seconds)
    {
        var (state, _, system, _) = Create();
        state.Botnet.AddNode();

        system.Update(TimeSpan.FromSeconds(seconds));

        Assert.Equal(0.0, state.Player.Money);
        Assert.Equal(0.0, state.Botnet.TotalEarned);
    }

    [Fact]
    public void Update_DoesNotTouchServers()
    {
        var (state, _, system, _) = Create();
        state.Botnet.AddNode();
        var home = state.Network.GetServer("home");
        var securityBefore = home.Security;

        system.Update(TimeSpan.FromSeconds(60));

        Assert.Equal(securityBefore, home.Security);
        Assert.Equal(0.0, home.Money);
    }

    private static (GameState State, BotnetService Service, BotnetSystem System, BotnetFormulas Formulas) Create(
        double startingMoney = 0)
    {
        var home = new Server(
            hostname: "home",
            maximumMoney: 0,
            startingMoney: 0,
            baseDifficulty: 5,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter: 0,
            maximumRam: 8,
            hasRootAccess: true);

        var network = new Network();
        network.AddServer(home);

        var state = new GameState(new Player("home", startingMoney: startingMoney), network);
        var formulas = new BotnetFormulas(new BalanceConfig());

        return (state, new BotnetService(state, formulas), new BotnetSystem(state, formulas), formulas);
    }
}