using NullByte.Core.Domain;
using static NullByte.Core.Tests.Infrastructure.TestFileSystem;

namespace NullByte.Core.Tests.Domain;

public sealed class VirtualFileSystemRestoreTests
{
    private static readonly DateTimeOffset Created = Start.AddDays(-2);
    private static readonly DateTimeOffset Modified = Start.AddDays(-1);

    [Fact]
    public void Restoring_a_file_keeps_its_times_and_raises_no_event()
    {
        var (files, _) = Create();
        var changes = Record(files);

        files.RestoreFile("home", "a.cx", "x", Created, Modified);

        Assert.True(files.TryGetFileTimes("home", "a.cx", out var times));
        Assert.Equal(new FileTimes(Created, Modified), times);
        Assert.Empty(changes);
    }

    [Fact]
    public void A_file_cannot_be_restored_as_modified_before_it_was_created()
    {
        var (files, _) = Create();

        Assert.Throws<ArgumentException>(() => files.RestoreFile("home", "a.cx", "x", Modified, Created));
    }

    [Fact]
    public void Restoring_a_file_creates_its_parent_directories_silently()
    {
        var (files, _) = Create();
        var changes = Record(files);

        files.RestoreFile("home", "a/b/c.cx", "x", Created, Modified);

        Assert.Equal(new[] { "a", "a/b" }, files.ListDirectories("home"));
        Assert.Empty(changes);
    }

    [Fact]
    public void Restoring_a_directory_creates_it_and_its_parents_silently()
    {
        var (files, _) = Create();
        var changes = Record(files);

        files.RestoreDirectory("home", "a/b");
        files.RestoreDirectory("home", "/");   // the root is always there

        Assert.Equal(new[] { "a", "a/b" }, files.ListDirectories("home"));
        Assert.Empty(changes);
    }

    [Fact]
    public void A_file_and_a_directory_cannot_be_restored_into_each_others_way()
    {
        var (files, _) = Create();
        files.RestoreFile("home", "a", "x", Created, Modified);

        Assert.Throws<InvalidOperationException>(() => files.RestoreDirectory("home", "a"));
        Assert.Throws<InvalidOperationException>(() => files.RestoreDirectory("home", "a/b"));
        Assert.Throws<InvalidOperationException>(() => files.RestoreFile("home", "a/b.cx", "x", Created, Modified));

        var (other, _) = Create();
        other.RestoreDirectory("home", "d");

        Assert.Throws<InvalidOperationException>(() => other.RestoreFile("home", "d", "x", Created, Modified));
    }

    [Fact]
    public void A_restored_file_keeps_working_like_any_other()
    {
        var (files, _) = Create();
        files.RestoreFile("home", "old.cx", "x", Created, Modified);                     // last changed yesterday
        files.RestoreFile("home", "future.cx", "x", Created, Start.AddHours(1));         // "modified" after the clock now

        files.WriteFile("home", "old.cx", "y");
        files.WriteFile("home", "future.cx", "y");

        files.TryGetFileTimes("home", "old.cx", out var old);
        Assert.Equal(Created, old.Created);
        Assert.Equal(Start, old.Modified);

        // Still strictly increasing, even when the restored time is ahead of the clock.
        files.TryGetFileTimes("home", "future.cx", out var future);
        Assert.Equal(Start.AddHours(1).AddMilliseconds(1), future.Modified);
    }

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