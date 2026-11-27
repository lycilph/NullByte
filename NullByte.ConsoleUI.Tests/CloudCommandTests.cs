using NullByte.ConsoleUI.Tests.Infrastructure;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;

namespace NullByte.ConsoleUI.Tests;

public sealed class CloudCommandTests
{
    private static ConsoleTestHarness CreateGame(double money, BalanceConfig? balance = null)
        => ConsoleTestSetup.CreateHarness(new ConsoleTestOptions { Money = money, Balance = balance });

    [Theory]
    [InlineData("cloud")]
    [InlineData("cloud list")]
    public async Task List_shows_empty_cloud_server_inventory(string commandLine)
    {
        var game = CreateGame(0);

        await game.RunAsync(commandLine);

        Assert.Contains("Cloud servers", game.Output);
        Assert.Contains("Servers: 0/5", game.Output);
        Assert.Contains("Next server costs $100,000", game.Output);
    }

    [Fact]
    public async Task Buy_creates_a_cloud_server_and_reports_the_cost()
    {
        var game = CreateGame(150_000);

        await game.RunAsync("cloud buy buildbox");

        var server = Assert.Single(game.State.Network.Servers.Where(server => server.IsCloudServer));
        Assert.Equal("buildbox", server.Hostname);
        Assert.Equal(8.0, server.MaximumRam);
        Assert.Equal(50_000.0, game.State.Player.Money);
        Assert.True(game.State.Player.DiscoveredServerIds.Contains(server.Id));
        Assert.True(game.State.Network.AreConnected("home", "buildbox"));
        Assert.Contains("Bought cloud server 'buildbox'", game.Output);
    }

    [Fact]
    public async Task Buy_accepts_an_initial_ram_tier_and_charges_the_combined_cost()
    {
        var game = CreateGame(200_000);

        await game.RunAsync("cloud buy buildbox 16");

        var server = Assert.Single(game.State.Network.Servers.Where(server => server.IsCloudServer));
        Assert.Equal(16.0, server.MaximumRam);
        Assert.Equal(50_000.0, game.State.Player.Money);
    }

    [Fact]
    public async Task Buy_with_insufficient_funds_does_not_create_a_server_or_spend_money()
    {
        var game = CreateGame(99_999);

        await game.RunAsync("cloud buy buildbox");

        Assert.Empty(game.State.Network.Servers.Where(server => server.IsCloudServer));
        Assert.Equal(99_999.0, game.State.Player.Money);
        Assert.Contains("Not enough money", game.Output);
    }

    [Fact]
    public async Task Upgrade_changes_ram_and_charges_the_configured_cost()
    {
        var game = CreateGame(200_000);
        await game.RunAsync("cloud buy buildbox");

        await game.RunAsync("cloud upgrade buildbox 16");

        var server = game.State.Network.GetServer("buildbox");
        Assert.Equal(16.0, server.MaximumRam);
        Assert.Equal(50_000.0, game.State.Player.Money);
        Assert.Contains("Upgraded 'buildbox' to 16 GB RAM", game.Output);
    }

    [Fact]
    public async Task Rename_updates_the_hostname_without_replacing_the_server()
    {
        var game = CreateGame(150_000);
        await game.RunAsync("cloud buy buildbox");
        var original = game.State.Network.GetServer("buildbox");

        await game.RunAsync("cloud rename buildbox buildbox-2");

        var renamed = game.State.Network.GetServer("buildbox-2");
        Assert.Same(original, renamed);
        Assert.False(game.State.Network.TryGetServer("buildbox", out _));
        Assert.True(game.State.Network.AreConnected("home", "buildbox-2"));
        Assert.Contains("Renamed cloud server to 'buildbox-2'", game.Output);
    }

    [Fact]
    public async Task Rename_rejects_non_cloud_servers()
    {
        var game = CreateGame(0);

        await game.RunAsync("cloud rename home renamed-home");

        Assert.True(game.State.Network.TryGetServer("home", out _));
        Assert.Contains("Unknown cloud server", game.Output);
    }

    [Theory]
    [InlineData("cloud buy")]
    [InlineData("cloud buy buildbox 12")]
    [InlineData("cloud buy buildbox extra")]
    [InlineData("cloud upgrade")]
    [InlineData("cloud upgrade buildbox")]
    [InlineData("cloud upgrade buildbox sixteen")]
    [InlineData("cloud rename buildbox")]
    [InlineData("cloud rename buildbox new-name extra")]
    [InlineData("cloud list extra")]
    [InlineData("cloud unknown")]
    public async Task Invalid_input_shows_usage_and_changes_nothing(string commandLine)
    {
        var game = CreateGame(1_000_000);

        await game.RunAsync(commandLine);

        Assert.Empty(game.State.Network.Servers.Where(server => server.IsCloudServer));
        Assert.Equal(1_000_000.0, game.State.Player.Money);
        Assert.Contains("Usage:", game.Output);
    }
}
