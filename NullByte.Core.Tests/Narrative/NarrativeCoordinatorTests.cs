using NullByte.Core.Domain;
using NullByte.Core.Narrative;
using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Narrative;

public sealed class NarrativeCoordinatorTests
{
    [Fact]
    public void Initialize_TutorialUnlocksOnlyPlayerInspectionAndPublishesOnce()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { ProgressionMode = NarrativeProgressionMode.Tutorial });
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);
        var messages = new List<NarrativeMessage>();
        coordinator.MessagePublished += messages.Add;

        coordinator.Initialize();
        coordinator.Initialize();

        Assert.True(world.State.Narrative.HasCapability("player.inspect"));
        Assert.False(world.State.Narrative.HasCapability("host.controls"));
        Assert.Single(messages);
        Assert.Equal("system.initialization", messages[0].Id);
        Assert.Contains("tutorial.initialization.complete", world.State.Narrative.CompletedMilestones);
    }

    [Fact]
    public void Initialize_FreeplayDoesNotPublishOrUnlockTutorialCapabilities()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { ProgressionMode = NarrativeProgressionMode.Tutorial });
        world.State.Narrative.Mode = NarrativeProgressionMode.Freeplay;
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);
        var messages = new List<NarrativeMessage>();
        coordinator.MessagePublished += messages.Add;

        coordinator.Initialize();
        coordinator.StatusUsed();
        world.NetworkService.Scan("home");

        Assert.Empty(messages);
        Assert.Empty(world.State.Narrative.CompletedMilestones);
        Assert.Empty(world.State.Narrative.UnlockedCapabilities);
    }

    [Fact]
    public void StatusUsed_UnlocksHostControlsAndPublishesOnlyOnce()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { ProgressionMode = NarrativeProgressionMode.Tutorial });
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);
        var messages = new List<NarrativeMessage>();
        coordinator.MessagePublished += messages.Add;

        coordinator.StatusUsed();
        coordinator.StatusUsed();

        Assert.True(world.State.Narrative.HasCapability("host.controls"));
        Assert.Single(messages);
        Assert.Equal("system.host-controls", messages[0].Id);
    }

    [Fact]
    public void FirstScanWithNewDiscovery_UnlocksAnalysisButRepeatedScanDoesNotRepublish()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Tutorial,
            Servers = [TestServers.Home(), TestServers.Create("n00dles")]
        });
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);
        var messages = new List<NarrativeMessage>();
        coordinator.MessagePublished += messages.Add;

        world.NetworkService.Scan("home");
        world.NetworkService.Scan("home");

        Assert.True(world.State.Narrative.HasCapability("network.analyze"));
        Assert.Single(messages);
        Assert.Equal("system.network-discovered", messages[0].Id);
        Assert.Contains("n00dles", messages[0].Text);
        Assert.Contains("Network scan complete.", messages[0].Text);
    }

    [Fact]
    public void SuccessfulNuke_UnlocksServerOperations()
    {
        var target = TestServers.Create("n00dles", hasRootAccess: false);
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { ProgressionMode = NarrativeProgressionMode.Tutorial, Servers = [TestServers.Home(), target] });
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);
        var messages = new List<NarrativeMessage>();
        coordinator.MessagePublished += messages.Add;

        var result = world.ServerActions.Nuke(target.Hostname);

        Assert.True(result.Succeeded);
        Assert.True(world.State.Narrative.HasCapability("server.operations"));
        Assert.True(world.State.Narrative.HasCapability("server.backdoor"));
        Assert.False(world.State.Narrative.HasCapability("files.navigation"));
        Assert.False(world.State.Narrative.HasCapability("files.manage"));
        Assert.Single(messages);
        Assert.Equal("system.root-access", messages[0].Id);
        Assert.Contains("n00dles", messages[0].Text);
        Assert.Contains("Host security is now under your control.", messages[0].Text);
    }

    [Fact]
    public void MessageObserverFailure_DoesNotBreakGameplayEvent()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Tutorial,
            Servers = [TestServers.Home(), TestServers.Create("n00dles")]
        });
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);
        coordinator.MessagePublished += _ => throw new InvalidOperationException("observer failed");

        var discovered = world.NetworkService.Scan("home");

        Assert.NotEmpty(discovered);
        Assert.True(world.State.Narrative.HasCapability("network.analyze"));
        Assert.Contains("tutorial.network.first-scan", world.State.Narrative.CompletedMilestones);
    }

    [Fact]
    public void Dispose_DetachesGameplayEventSubscriptions()
    {
        var target = TestServers.Create("n00dles", hasRootAccess: false);
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Tutorial,
            Servers = [TestServers.Home(), target]
        });
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);
        var messages = new List<NarrativeMessage>();
        coordinator.MessagePublished += messages.Add;

        coordinator.Dispose();
        world.ServerActions.Nuke(target.Hostname);

        Assert.Empty(messages);
        Assert.Empty(world.State.Narrative.CompletedMilestones);
    }

    [Fact]
    public void FileRead_DefaultCatalogRecordsFirstRecoveredDiscovery()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { ProgressionMode = NarrativeProgressionMode.Tutorial });
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions);

        Assert.True(coordinator.FileRead("N00DLES", "/etc/system/profile.cfg"));
        Assert.False(coordinator.FileRead("n00dles", "etc/system/profile.cfg"));

        Assert.Contains("archive.project-profile", world.State.Narrative.DiscoveredDiscoveries);
        Assert.Empty(world.State.Narrative.UnlockedCapabilities);
        Assert.Empty(world.State.Narrative.CompletedMilestones);
    }

    [Fact]
    public void FileRead_RecordsOptionalAndRequiredDiscoveriesOnceWithoutUnlockingCapabilities()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { ProgressionMode = NarrativeProgressionMode.Tutorial });
        var catalog = new NarrativeContentCatalog([], 
        [
            new NarrativeDiscovery("archive.optional", "home", "archive/optional.log", NarrativeDiscoveryRequirement.Optional),
            new NarrativeDiscovery("archive.required", "home", "archive/required.log", NarrativeDiscoveryRequirement.Required)
        ]);
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions, catalog);

        Assert.True(coordinator.FileRead("home", "archive/optional.log"));
        Assert.False(coordinator.FileRead("home", "/archive/optional.log"));
        Assert.True(coordinator.FileRead("home", "archive/required.log"));

        Assert.Contains("archive.optional", world.State.Narrative.DiscoveredDiscoveries);
        Assert.Contains("archive.required", world.State.Narrative.DiscoveredDiscoveries);
        Assert.Empty(world.State.Narrative.UnlockedCapabilities);
        Assert.Empty(world.State.Narrative.CompletedMilestones);
    }

    [Fact]
    public void FileRead_AlsoRecordsDiscoveriesInFreeplay()
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { ProgressionMode = NarrativeProgressionMode.Tutorial });
        world.State.Narrative.Mode = NarrativeProgressionMode.Freeplay;
        var catalog = new NarrativeContentCatalog([], 
        [new NarrativeDiscovery("archive.record", "home", "archive/record.log", NarrativeDiscoveryRequirement.Optional)]);
        var coordinator = new NarrativeCoordinator(world.State, world.NetworkService, world.ServerActions, catalog);

        Assert.True(coordinator.FileRead("home", "archive/record.log"));
        Assert.Contains("archive.record", world.State.Narrative.DiscoveredDiscoveries);
        Assert.Empty(world.State.Narrative.CompletedMilestones);
    }

}