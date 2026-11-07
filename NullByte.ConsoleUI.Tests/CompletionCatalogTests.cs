using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;
using NullByte.Core.World;

namespace NullByte.ConsoleUI.Tests;

public sealed class CompletionCatalogTests
{
    [Fact]
    public void BeforeRefresh_NothingIsSuggested()
    {
        var fixture = CreateFixture();

        Assert.False(fixture.Catalog.Complete("h", 1).HasCandidates);
    }

    [Fact]
    public void Commands_IncludeExitAndQuit()
    {
        var fixture = CreateFixture();
        fixture.Catalog.Refresh();

        var result = fixture.Catalog.Complete("qu", 2);

        Assert.Equal("quit ", result.Replacement);
    }

    [Fact]
    public void Commands_HideDebugCommandsExceptDebugItself()
    {
        var fixture = CreateFixture();
        fixture.Catalog.Refresh();

        Assert.False(fixture.Catalog.Complete("thr", 3).HasCandidates);
        Assert.Equal("debug ", fixture.Catalog.Complete("deb", 3).Replacement);
    }

    [Fact]
    public void Connect_OffersNothingUntilNeighborsAreScanned()
    {
        var fixture = CreateFixture();
        fixture.Catalog.Refresh();

        Assert.False(fixture.Catalog.Complete("connect ", 8).HasCandidates);
    }

    [Fact]
    public void Connect_OffersScannedNeighborsAfterRefresh()
    {
        var fixture = CreateFixture();
        var neighbors = fixture.Network.Scan().Select(server => server.Hostname).ToArray();

        // The catalog is a snapshot: it must not change until refreshed.
        var resultBeforeRefresh = fixture.Catalog.Complete("connect ", 8);
        Assert.False(resultBeforeRefresh.HasCandidates);

        fixture.Catalog.Refresh();
        var result = fixture.Catalog.Complete("connect ", 8);

        Assert.Equal(
            neighbors.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            result.Candidates);
    }

    [Fact]
    public void Connect_NeverOffersTheCurrentServer()
    {
        var fixture = CreateFixture();
        var neighbor = fixture.Network.Scan()[0];
        fixture.Network.Connect(neighbor.Hostname);
        fixture.Catalog.Refresh();

        var result = fixture.Catalog.Complete("connect ", 8);

        Assert.DoesNotContain(neighbor.Hostname, result.Candidates);
    }

    [Fact]
    public void Analyze_OffersEveryDiscoveredServer()
    {
        var fixture = CreateFixture();
        var neighbors = fixture.Network.Scan();
        fixture.Catalog.Refresh();

        var result = fixture.Catalog.Complete("analyze ", 8);

        // Home plus everything the scan found.
        Assert.Equal(neighbors.Count + 1, result.Candidates.Count);
        Assert.Contains("home", result.Candidates);
    }

    private static Fixture CreateFixture()
    {
        var gameState = InitialGameStateFactory.Create();
        var registry = CommandRegistryFactory.Create();

        return new Fixture(
            new NetworkService(gameState),
            new CompletionCatalog(registry, gameState));
    }

    private sealed record Fixture(NetworkService Network, CompletionCatalog Catalog);
}