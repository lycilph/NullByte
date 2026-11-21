using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class VirtualFileSystemTimingTests
{
    [Fact]
    public void RestoreFile_PreservesContentAndTimestamps()
    {
        var network = new Network();
        network.AddServer(CreateServer("home"));

        var fileSystem = new VirtualFileSystem(network);

        var created = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var modified = new DateTimeOffset(2026, 1, 3, 6, 7, 8, TimeSpan.Zero);

        fileSystem.RestoreFile(
            "home",
            "script.cs",
            "print(\"hello\");",
            created,
            modified);

        Assert.True(
            fileSystem.TryReadFile("home", "script.cs", out var content));

        Assert.Equal("print(\"hello\");", content);

        Assert.True(
            fileSystem.TryGetFileTimes("home", "script.cs", out var times));

        Assert.Equal(created, times.Created);
        Assert.Equal(modified, times.Modified);
    }

    [Fact]
    public void RestoreFile_WhenModifiedIsEarlierThanCreated_Throws()
    {
        var network = new Network();
        network.AddServer(CreateServer("home"));

        var fileSystem = new VirtualFileSystem(network);

        var created = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        var modified = created.AddSeconds(-1);

        Assert.Throws<ArgumentException>(() =>
            fileSystem.RestoreFile(
                "home",
                "script.cs",
                "content",
                created,
                modified));
    }

    [Fact]
    public void RestoreFile_DoesNotRaiseChangedEvent()
    {
        var network = new Network();
        network.AddServer(CreateServer("home"));

        var fileSystem = new VirtualFileSystem(network);
        var changes = new List<FileChange>();

        fileSystem.Changed += changes.Add;

        fileSystem.RestoreFile(
            "home",
            "script.cs",
            "print(\"hello\");",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 3, 6, 7, 8, TimeSpan.Zero));

        Assert.Empty(changes);
    }

    private static Server CreateServer(string hostname)
        => new(
            hostname: hostname,
            maximumMoney: 100_000,
            startingMoney: 50_000,
            baseDifficulty: 5,
            minimumSecurity: 1,
            startingSecurity: 5,
            requiredHackingLevel: 1,
            growthParameter: 20,
            maximumRam: 8);
}