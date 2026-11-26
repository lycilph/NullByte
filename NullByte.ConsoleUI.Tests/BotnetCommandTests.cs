using NullByte.ConsoleUI.Tests.Infrastructure;
using NullByte.Core.Configuration;

namespace NullByte.ConsoleUI.Tests;

public sealed class BotnetCommandTests
{
    private static ConsoleTestHarness CreateGame(double money, BalanceConfig? balance = null)
        => ConsoleTestSetup.CreateHarness(new ConsoleTestOptions { Money = money, Balance = balance });

    [Theory]
    [InlineData("botnet")]
    [InlineData("botnet status")]
    public async Task Status_without_nodes_shows_an_empty_botnet(string commandLine)
    {
        var game = CreateGame(money: 0);

        await game.RunAsync(commandLine);

        Assert.Contains("Nodes: 0", game.Output);
        Assert.Contains("Next node costs", game.Output);
    }

    [Fact]
    public async Task Buying_with_enough_money_buys_the_node()
    {
        var game = CreateGame(money: 1_500);

        await game.RunAsync("botnet buy");

        Assert.Equal(1, game.State.Botnet.NodeCount);
        Assert.Equal(500.0, game.State.Player.Money);
        Assert.Contains("Bought botnet node #1", game.Output);
    }

    [Fact]
    public async Task Buying_with_too_little_money_says_so_and_buys_nothing()
    {
        var game = CreateGame(money: 10);

        await game.RunAsync("botnet buy");

        Assert.Equal(0, game.State.Botnet.NodeCount);
        Assert.Equal(10.0, game.State.Player.Money);
        Assert.Contains("Not enough money", game.Output);
    }

    [Theory]
    [InlineData("level", 2, 1, 1)]
    [InlineData("ram", 1, 2, 1)]
    [InlineData("core", 1, 1, 2)]
    [InlineData("cores", 1, 1, 2)]
    [InlineData("LEVEL", 2, 1, 1)]
    public async Task Upgrades_use_node_numbers_counted_from_one(string kind, int level, int ram, int cores)
    {
        var game = CreateGame(money: 10_000_000);
        game.State.Botnet.AddNode();
        game.State.Botnet.AddNode();

        await game.RunAsync($"botnet upgrade 2 {kind}");

        var untouched = game.State.Botnet.Nodes[0];
        var upgraded = game.State.Botnet.Nodes[1];

        Assert.Equal(1, untouched.Level);
        Assert.Equal(level, upgraded.Level);
        Assert.Equal(ram, upgraded.Ram);
        Assert.Equal(cores, upgraded.Cores);
        Assert.Contains("Upgraded node #2", game.Output);
    }

    [Theory]
    [InlineData("botnet upgrade 9 level")]
    [InlineData("botnet upgrade 0 level")]
    public async Task Upgrading_a_missing_node_says_so(string commandLine)
    {
        var game = CreateGame(money: 10_000_000);
        game.State.Botnet.AddNode();

        await game.RunAsync(commandLine);

        Assert.Contains("no such botnet node", game.Output);
        Assert.Equal(10_000_000.0, game.State.Player.Money);
    }

    [Theory]
    [InlineData("botnet upgrade")]
    [InlineData("botnet upgrade 1")]
    [InlineData("botnet upgrade one level")]
    [InlineData("botnet upgrade 1 speed")]
    [InlineData("botnet upgrade 1 level extra")]
    [InlineData("botnet buy now")]
    [InlineData("botnet status extra")]
    [InlineData("botnet frobnicate")]
    public async Task Invalid_input_shows_the_usage_and_changes_nothing(string commandLine)
    {
        var game = CreateGame(money: 10_000_000);
        game.State.Botnet.AddNode();

        await game.RunAsync(commandLine);

        Assert.Contains("Usage:", game.Output);
        Assert.Equal(10_000_000.0, game.State.Player.Money);
        Assert.Equal(1, game.State.Botnet.NodeCount);
        Assert.Equal(1, game.State.Botnet.Nodes[0].Level);
    }

    [Fact]
    public async Task Status_lists_each_node_and_marks_maxed_upgrades()
    {
        var game = CreateGame(money: 0, new BalanceConfig { BotnetMaxLevel = 1 });
        game.State.Botnet.AddNode();
        game.State.Botnet.AddNode();

        await game.RunAsync("botnet status");

        Assert.Contains("Nodes: 2", game.Output);
        Assert.Contains("max", game.Output);
    }
}
