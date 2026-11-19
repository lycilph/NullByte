using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Formulas;
using NullByte.Core.Persistence;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Core.World;
using NullByte.Shell;
using NullByte.Shell.Persistence;

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
        var home = fixture.Network.GetCurrentServer().Hostname;
        var neighbors = fixture.Network.Scan(home).Select(server => server.Hostname).ToArray();

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
        var home = fixture.Network.GetCurrentServer().Hostname;
        var neighbor = fixture.Network.Scan(home)[0];
        fixture.Network.Connect(neighbor.Hostname);
        fixture.Catalog.Refresh();

        var result = fixture.Catalog.Complete("connect ", 8);

        Assert.DoesNotContain(neighbor.Hostname, result.Candidates);
    }

    [Fact]
    public void Analyze_OffersEveryDiscoveredServer()
    {
        var fixture = CreateFixture();
        var home = fixture.Network.GetCurrentServer().Hostname;
        var neighbors = fixture.Network.Scan(home);
        fixture.Catalog.Refresh();

        var result = fixture.Catalog.Complete("analyze ", 8);

        // Home plus everything the scan found.
        Assert.Equal(neighbors.Count + 1, result.Candidates.Count);
        Assert.Contains("home", result.Candidates);
    }

    private static Fixture CreateFixture()
    {
        var terminal = new FakeTerminal();
        var gameState = InitialGameStateFactory.Create();
        var session = GameSession.Create(new GameSessionOptions() { Output = terminal });
        var registry = CommandSetup.Create(session, terminal);
        var scheduler = new GameScheduler(new FakeGameClock());
        var formulas = new NetworkFormulas(new Core.Configuration.BalanceConfig());

        var fileSaveStore = new FileSaveStore("Saves");
        var saveSerializer = new SaveGameSerializer();
        var saveManager = new SaveManager(fileSaveStore, saveSerializer);

        return new Fixture(
            new NetworkService(gameState, scheduler, formulas),
            new CompletionCatalog(registry, gameState, saveManager));
    }

    private sealed record Fixture(NetworkService Network, CompletionCatalog Catalog);
}