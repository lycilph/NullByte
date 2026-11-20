using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class VirtualFileSystemTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    // A clock the test moves by hand.
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
        public void Set(DateTimeOffset value) => now = value;
    }

    private static (VirtualFileSystem Files, ManualTimeProvider Clock) Create()
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));

        var clock = new ManualTimeProvider(Start);
        return (new VirtualFileSystem(network, clock), clock);
    }

    private static Server NewServer(string hostname) => new(
        hostname, maximumMoney: 0, startingMoney: 0, baseDifficulty: 1, minimumSecurity: 1,
        startingSecurity: 1, requiredHackingLevel: 1, growthParameter: 0, maximumRam: 8);

    private static List<FileChange> Record(VirtualFileSystem files)
    {
        var changes = new List<FileChange>();
        files.Changed += changes.Add;
        return changes;
    }

    // ---- times ----

    [Fact]
    public void A_new_file_gets_the_same_created_and_modified_time()
    {
        var (files, _) = Create();

        files.WriteFile("home", "a.cx", "x");

        Assert.True(files.TryGetFileTimes("home", "a.cx", out var times));
        Assert.Equal(Start, times.Created);
        Assert.Equal(Start, times.Modified);
    }

    [Fact]
    public void Overwriting_keeps_created_and_updates_modified()
    {
        var (files, clock) = Create();
        files.WriteFile("home", "a.cx", "x");

        clock.Advance(TimeSpan.FromSeconds(5));
        files.WriteFile("home", "a.cx", "y");

        files.TryGetFileTimes("home", "a.cx", out var times);
        Assert.Equal(Start, times.Created);
        Assert.Equal(Start.AddSeconds(5), times.Modified);
    }

    [Fact]
    public void Writes_in_the_same_millisecond_still_increase_modified()
    {
        var (files, _) = Create();   // the clock never moves

        files.WriteFile("home", "a.cx", "x");
        files.WriteFile("home", "a.cx", "y");
        files.WriteFile("home", "a.cx", "z");

        files.TryGetFileTimes("home", "a.cx", out var times);
        Assert.Equal(Start.AddMilliseconds(2), times.Modified);
    }

    [Fact]
    public void Modified_never_goes_backwards_when_the_clock_does()
    {
        var (files, clock) = Create();
        files.WriteFile("home", "a.cx", "x");

        clock.Set(Start.AddHours(-1));
        files.WriteFile("home", "a.cx", "y");

        files.TryGetFileTimes("home", "a.cx", out var times);
        Assert.Equal(Start.AddMilliseconds(1), times.Modified);
    }

    [Fact]
    public void Times_are_whole_milliseconds()
    {
        var (files, clock) = Create();
        clock.Set(Start.AddTicks(1234));   // finer than a millisecond

        files.WriteFile("home", "a.cx", "x");

        files.TryGetFileTimes("home", "a.cx", out var times);
        Assert.Equal(Start, times.Created);
    }

    [Fact]
    public void Renaming_keeps_both_times()
    {
        var (files, clock) = Create();
        files.WriteFile("home", "a.cx", "x");
        clock.Advance(TimeSpan.FromMinutes(1));
        files.WriteFile("home", "a.cx", "y");
        files.TryGetFileTimes("home", "a.cx", out var before);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(files.MoveFile("home", "a.cx", "b.cx"));

        Assert.False(files.TryGetFileTimes("home", "a.cx", out _));
        Assert.True(files.TryGetFileTimes("home", "b.cx", out var after));
        Assert.Equal(before, after);
    }

    [Fact]
    public void Copied_files_get_fresh_times_on_the_destination()
    {
        var (files, clock) = Create();
        files.WriteFile("home", "a.cx", "x");
        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.True(files.CopyFiles("home", "n00dles", ["a.cx"]));

        files.TryGetFileTimes("n00dles", "a.cx", out var copy);
        Assert.Equal(Start.AddMinutes(10), copy.Created);
        Assert.Equal(Start.AddMinutes(10), copy.Modified);
    }

    [Fact]
    public void Times_of_a_missing_file_are_not_available()
    {
        var (files, _) = Create();

        Assert.False(files.TryGetFileTimes("home", "nope.cx", out _));
        Assert.Throws<KeyNotFoundException>(() => { files.TryGetFileTimes("nowhere", "a.cx", out _); });
    }

    // ---- change events ----

    [Fact]
    public void Changes_are_reported_as_created_changed_and_deleted()
    {
        var (files, _) = Create();
        var changes = Record(files);

        files.WriteFile("home", "a.cx", "x");
        files.WriteFile("home", "a.cx", "y");
        files.DeleteFile("home", "a.cx");

        Assert.Equal(
            new[]
            {
                new FileChange("home", "a.cx", FileChangeKind.Created),
                new FileChange("home", "a.cx", FileChangeKind.Changed),
                new FileChange("home", "a.cx", FileChangeKind.Deleted),
            },
            changes);
    }

    [Fact]
    public void Renaming_reports_a_delete_and_a_create()
    {
        var (files, _) = Create();
        files.WriteFile("home", "a.cx", "x");
        var changes = Record(files);

        files.MoveFile("home", "a.cx", "b.cx");

        Assert.Equal(
            new[]
            {
                new FileChange("home", "a.cx", FileChangeKind.Deleted),
                new FileChange("home", "b.cx", FileChangeKind.Created),
            },
            changes);
    }

    [Fact]
    public void Operations_that_change_nothing_report_nothing()
    {
        var (files, _) = Create();
        files.WriteFile("home", "a.cx", "x");
        files.WriteFile("home", "b.cx", "y");
        var changes = Record(files);

        Assert.False(files.DeleteFile("home", "missing.cx"));
        Assert.False(files.MoveFile("home", "missing.cx", "c.cx"));
        Assert.False(files.MoveFile("home", "a.cx", "b.cx"));   // the target is in use
        Assert.True(files.MoveFile("home", "a.cx", "a.cx"));    // same name: nothing to do

        Assert.Empty(changes);
    }

    [Fact]
    public void Events_use_the_servers_own_spelling_of_the_hostname()
    {
        var (files, _) = Create();
        var changes = Record(files);

        files.WriteFile("HOME", "a.cx", "x");

        Assert.Equal("home", Assert.Single(changes).Hostname);
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