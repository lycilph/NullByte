using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class ServerTests
{
    [Fact]
    public void Constructor_InitializesServer()
    {
        var server = CreateServer();

        Assert.Equal("n00dles", server.Id);
        Assert.Equal("n00dles", server.Hostname);

        Assert.Equal(100_000, server.MaximumMoney);
        Assert.Equal(50_000, server.Money);

        Assert.Equal(1, server.MinimumSecurity);
        Assert.Equal(5, server.Security);

        Assert.Equal(10, server.RequiredHackingLevel);
        Assert.Equal(20, server.GrowthParameter);

        Assert.Equal(8, server.MaximumRam);
        Assert.Equal(0, server.UsedRam);
        Assert.Equal(8, server.AvailableRam);

        Assert.False(server.HasRootAccess);
    }

    [Fact]
    public void GrantRootAccess_GrantsRootAccess()
    {
        var server = CreateServer();

        server.GrantRootAccess();

        Assert.True(server.HasRootAccess);
    }

    [Fact]
    public void RemoveMoney_RemovesRequestedAmount()
    {
        var server = CreateServer();

        var removed = server.RemoveMoney(10_000);

        Assert.Equal(10_000, removed);
        Assert.Equal(40_000, server.Money);
    }

    [Fact]
    public void RemoveMoney_CannotReduceMoneyBelowZero()
    {
        var server = CreateServer();

        var removed = server.RemoveMoney(100_000);

        Assert.Equal(50_000, removed);
        Assert.Equal(0, server.Money);
    }

    [Fact]
    public void AddMoney_AddsRequestedAmount()
    {
        var server = CreateServer();

        var added = server.AddMoney(10_000);

        Assert.Equal(10_000, added);
        Assert.Equal(60_000, server.Money);
    }

    [Fact]
    public void AddMoney_CannotExceedMaximumMoney()
    {
        var server = CreateServer();

        var added = server.AddMoney(100_000);

        Assert.Equal(50_000, added);
        Assert.Equal(100_000, server.Money);
    }

    [Fact]
    public void IncreaseSecurity_IncreasesSecurity()
    {
        var server = CreateServer();

        server.IncreaseSecurity(2);

        Assert.Equal(7, server.Security);
    }

    [Fact]
    public void DecreaseSecurity_DecreasesSecurity()
    {
        var server = CreateServer();

        var removed = server.DecreaseSecurity(2);

        Assert.Equal(2, removed);
        Assert.Equal(3, server.Security);
    }

    [Fact]
    public void DecreaseSecurity_CannotGoBelowMinimumSecurity()
    {
        var server = CreateServer();

        var removed = server.DecreaseSecurity(100);

        Assert.Equal(4, removed);
        Assert.Equal(1, server.Security);
    }

    [Fact]
    public void AllocateRam_IncreasesUsedRam()
    {
        var server = CreateServer();

        server.AllocateRam(3);

        Assert.Equal(3, server.UsedRam);
        Assert.Equal(5, server.AvailableRam);
    }

    [Fact]
    public void AllocateRam_WhenInsufficientRam_Throws()
    {
        var server = CreateServer();

        Assert.Throws<InvalidOperationException>(
            () => server.AllocateRam(9));

        Assert.Equal(0, server.UsedRam);
    }

    [Fact]
    public void ReleaseRam_DecreasesUsedRam()
    {
        var server = CreateServer();

        server.AllocateRam(6);

        server.ReleaseRam(2);

        Assert.Equal(4, server.UsedRam);
        Assert.Equal(4, server.AvailableRam);
    }

    [Fact]
    public void ReleaseRam_MoreThanAllocated_Throws()
    {
        var server = CreateServer();

        server.AllocateRam(2);

        Assert.Throws<InvalidOperationException>(
            () => server.ReleaseRam(3));

        Assert.Equal(2, server.UsedRam);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void MoneyMutation_WithInvalidAmount_Throws(
        double amount)
    {
        var server = CreateServer();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => server.AddMoney(amount));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => server.RemoveMoney(amount));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SecurityMutation_WithInvalidAmount_Throws(
        double amount)
    {
        var server = CreateServer();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => server.IncreaseSecurity(amount));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => server.DecreaseSecurity(amount));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RamMutation_WithInvalidAmount_Throws(
        double amount)
    {
        var server = CreateServer();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => server.AllocateRam(amount));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => server.ReleaseRam(amount));
    }

    private static Server CreateServer()
    {
        return new Server(
            id: "n00dles",
            hostname: "n00dles",
            maximumMoney: 100_000,
            startingMoney: 50_000,
            minimumSecurity: 1,
            startingSecurity: 5,
            requiredHackingLevel: 10,
            growthParameter: 20,
            maximumRam: 8);
    }
}