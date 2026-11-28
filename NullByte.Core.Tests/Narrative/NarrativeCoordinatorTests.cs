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
}