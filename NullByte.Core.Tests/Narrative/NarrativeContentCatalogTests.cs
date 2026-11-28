using NullByte.Core.Narrative;

namespace NullByte.Core.Tests.Narrative;

public sealed class NarrativeContentCatalogTests
{
    [Fact]
    public void Default_ContainsFirstRecoveredDiscovery()
    {
        Assert.True(NarrativeContentCatalog.Default.TryGetDiscovery("N00DLES", "/etc/system/profile.cfg", out var discovery));
        Assert.Equal("archive.project-profile", discovery!.Id);
        Assert.Equal(NarrativeDiscoveryRequirement.Optional, discovery.Requirement);
    }

    [Fact]
    public void Default_ContainsEveryImplementedTutorialMilestone()
    {
        var expected = new[]
        {
            "tutorial.initialization.complete",
            "tutorial.status.used",
            "tutorial.network.first-scan",
            "tutorial.network.first-analysis",
            "tutorial.network.first-connection",
            "tutorial.network.first-root",
            "tutorial.network.first-hack",
            "tutorial.expansion.first-script"
        };

        Assert.Equal(expected.Order(), NarrativeContentCatalog.Default.Entries.Select(entry => entry.MilestoneId).Order());
    }

    [Fact]
    public void Constructor_RejectsDuplicateContentIds()
    {
        var entries = new[]
        {
            new NarrativeEntry("same", NarrativeAct.Initialization, "milestone.one", "First")
        };
        var discoveries = new[]
        {
            new NarrativeDiscovery("same", "home", "one.log", NarrativeDiscoveryRequirement.Optional)
        };

        Assert.Throws<ArgumentException>(() => new NarrativeContentCatalog(entries, discoveries));
    }

    [Fact]
    public void Constructor_RejectsDuplicateEntryIds()
    {
        var entries = new[]
        {
            new NarrativeEntry("same", NarrativeAct.Initialization, "milestone.one", "First"),
            new NarrativeEntry("same", NarrativeAct.Initialization, "milestone.two", "Second")
        };

        Assert.Throws<ArgumentException>(() => new NarrativeContentCatalog(entries));
    }

    [Fact]
    public void Constructor_RejectsDuplicateMilestoneIds()
    {
        var entries = new[]
        {
            new NarrativeEntry("entry.one", NarrativeAct.Initialization, "same", "First"),
            new NarrativeEntry("entry.two", NarrativeAct.Initialization, "same", "Second")
        };

        Assert.Throws<ArgumentException>(() => new NarrativeContentCatalog(entries));
    }

    [Fact]
    public void GetRequired_ThrowsForUnknownEntry()
    {
        Assert.Throws<KeyNotFoundException>(() => NarrativeContentCatalog.Default.GetRequired("missing"));
    }

    [Fact]
    public void Render_ReplacesProvidedValues()
    {
        var entry = new NarrativeEntry("entry", NarrativeAct.Initialization, "milestone", "Connected: {hostname}");

        Assert.Equal("Connected: n00dles", entry.Render(new Dictionary<string, string> { ["hostname"] = "n00dles" }));
    }
}
