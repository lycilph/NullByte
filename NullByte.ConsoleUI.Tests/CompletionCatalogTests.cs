using NSubstitute;
using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.ConsoleUI.Tests.Infrastructure;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Persistence;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Core.World;
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
    public void TutorialCompletion_OnlySuggestsUnlockedCommands()
    {
        var harness = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Tutorial
        });
        var saveStore = Substitute.For<ISaveStore>();
        saveStore.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SaveSlotInfo>>([]));
        var saveManager = new SaveManager(saveStore, new SaveGameSerializer());
        var catalog = new CompletionCatalog(
            harness.Registry,
            harness.State,
            harness.Session.Services.Activities.Catalog,
            saveManager);

        catalog.Refresh();

        Assert.Equal("status ", catalog.Complete("st", 2).Replacement);
        Assert.False(catalog.Complete("he", 2).HasCandidates);
        Assert.False(catalog.Complete("de", 2).HasCandidates);
    }

    [Fact]
    public void Study_OffersUniversityCoursesAndUniversities()
    {
        var fixture = CreateFixture();
        fixture.Catalog.Refresh();

        var courses = fixture.Catalog.Complete("study ", 6);
        var universities = fixture.Catalog.Complete("study algorithms ", 17);

        Assert.Contains("algorithms", courses.Candidates);
        Assert.DoesNotContain("strength", courses.Candidates);
        Assert.Contains("rothman-university", universities.Candidates);
        Assert.DoesNotContain("gym", universities.Candidates);
    }

    [Fact]
    public void Train_OffersGymCoursesAndGyms()
    {
        var fixture = CreateFixture();
        fixture.Catalog.Refresh();

        var courses = fixture.Catalog.Complete("train ", 6);
        var gyms = fixture.Catalog.Complete("train strength ", 15);

        Assert.Contains("strength", courses.Candidates);
        Assert.DoesNotContain("algorithms", courses.Candidates);
        Assert.Contains("iron-gym", gyms.Candidates);
        Assert.DoesNotContain("university", gyms.Candidates);
    }

    [Fact]
    public void Work_OffersCompaniesAndDistinctCareers()
    {
        var fixture = CreateFixture();
        fixture.Catalog.Refresh();

        var companies = fixture.Catalog.Complete("work ", 5);
        var careers = fixture.Catalog.Complete("work testcorp ", 15);

        Assert.NotEmpty(companies.Candidates);
        Assert.NotEmpty(careers.Candidates);
    }

    [Fact]
    public void Crime_OffersCrimes()
    {
        var fixture = CreateFixture();
        fixture.Catalog.Refresh();

        var result = fixture.Catalog.Complete("crime ", 6);

        Assert.NotEmpty(result.Candidates);
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

    [Fact]
    public void Connect_OffersBackdooredServersThatAreNotNeighbors()
    {
        var home = new Server("home", 0, 0, 1, 1, 1, 1, 1, 8, hasRootAccess: true);
        var near = new Server("near", 1000, 500, 3, 1, 3, 1, 30, 4);
        var far = new Server("far", 1000, 500, 3, 1, 3, 1, 30, 4, hasBackdoor: true);

        var network = new Network();
        network.AddServer(home);
        network.AddServer(near);
        network.AddServer(far);
        network.Connect("home", "near");
        network.Connect("near", "far");

        var player = new Player(network.GetServer("home").Id);
        player.DiscoverServer(network.GetServer("near").Id);
        player.DiscoverServer(network.GetServer("far").Id);

        var fixture = CreateFixture(new GameState(player, network));
        fixture.Catalog.Refresh();

        var result = fixture.Catalog.Complete("connect ", 8);

        Assert.Equal(new[] { "far", "near" }, result.Candidates);
    }

    [Fact]
    public void CloudCommands_OfferOwnedCloudServersForUpgradeAndRename()
    {
        var home = new Server("home", 0, 0, 1, 1, 1, 1, 1, 8, hasRootAccess: true);
        var cloud = new Server("buildbox", 0, 0, 1, 1, 1, 1, 0, 8, hasRootAccess: true, isCloudServer: true);
        var network = new Network();
        network.AddServer(home);
        network.AddServer(cloud);
        network.Connect("home", "buildbox");
        var player = new Player(home.Id);
        player.DiscoverServer(cloud.Id);

        var fixture = CreateFixture(new GameState(player, network));
        fixture.Catalog.Refresh();

        Assert.Equal("buildbox ", fixture.Catalog.Complete("cloud upgrade bu", 16).Replacement);
        Assert.Equal("buildbox ", fixture.Catalog.Complete("cloud rename bu", 15).Replacement);
    }

    private static Fixture CreateFixture(GameState? state = null)
    {
        var gameState = state ?? InitialGameStateFactory.Create();

        // The catalog reads command names and completions from the registry and the activities from the game's
        // catalog, so a default game provides both.
        var game = ConsoleTestSetup.CreateHarness();
        var scheduler = new GameScheduler(new FakeGameClock());
        var formulas = new NetworkFormulas(new Core.Configuration.BalanceConfig());

        // No save slots on disk, and nothing in this test touches the file system.
        var saveStore = Substitute.For<ISaveStore>();
        saveStore
            .ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SaveSlotInfo>>([]));
        var saveManager = new SaveManager(saveStore, new SaveGameSerializer());

        return new Fixture(
            new NetworkService(gameState, scheduler, formulas),
            new CompletionCatalog(game.Registry, gameState, game.Session.Services.Activities.Catalog, saveManager));
    }

    private sealed record Fixture(NetworkService Network, CompletionCatalog Catalog);
}