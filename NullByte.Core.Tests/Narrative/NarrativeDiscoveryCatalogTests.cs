using NullByte.Core.Narrative;

namespace NullByte.Core.Tests.Narrative;

public sealed class NarrativeContentCatalogDiscoveryTests
{
    [Fact]
    public void TryGet_NormalizesPathsAndMatchesHostnamesWithoutCaseSensitivity()
    {
        var catalog = new NarrativeContentCatalog([],
        [new NarrativeDiscovery("archive.record", "home", "archive/record.log", NarrativeDiscoveryRequirement.Optional)]);

        Assert.True(catalog.TryGetDiscovery("HOME", "/archive/record.log", out var discovery));
        Assert.Equal("archive.record", discovery!.Id);
        Assert.Equal("archive/record.log", discovery.Path);
        Assert.Equal(NarrativeDiscoveryRequirement.Optional, discovery.Requirement);
    }

    [Fact]
    public void TryGet_DoesNotTreatFilePathsAsCaseInsensitive()
    {
        var catalog = new NarrativeContentCatalog(
        [],
        [new NarrativeDiscovery("archive.record", "home", "Archive/Record.log", NarrativeDiscoveryRequirement.Required)]);

        Assert.False(catalog.TryGetDiscovery("home", "archive/record.log", out _));
    }

    [Fact]
    public void Constructor_RejectsDuplicateDiscoveryIds()
    {
        var discoveries = new[]
        {
            new NarrativeDiscovery("same", "home", "one.log", NarrativeDiscoveryRequirement.Optional),
            new NarrativeDiscovery("same", "home", "two.log", NarrativeDiscoveryRequirement.Required)
        };

        Assert.Throws<ArgumentException>(() => new NarrativeContentCatalog([], discoveries));
    }

    [Fact]
    public void Constructor_RejectsDuplicateFileRegistrations()
    {
        var discoveries = new[]
        {
            new NarrativeDiscovery("one", "home", "archive/record.log", NarrativeDiscoveryRequirement.Optional),
            new NarrativeDiscovery("two", "HOME", "/archive/record.log", NarrativeDiscoveryRequirement.Required)
        };

        Assert.Throws<ArgumentException>(() => new NarrativeContentCatalog([], discoveries));
    }
}
