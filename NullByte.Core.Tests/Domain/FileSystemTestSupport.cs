// FileSystemTestSupport.cs
using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan by) => now += by;
    public void Set(DateTimeOffset value) => now = value;
}

internal static class FileSystemTestSupport
{
    public static readonly DateTimeOffset Start = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    public static (VirtualFileSystem Files, ManualTimeProvider Clock) Create()
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));

        var clock = new ManualTimeProvider(Start);
        return (new VirtualFileSystem(network, clock), clock);
    }

    public static Server NewServer(string hostname) => new(
        hostname, maximumMoney: 0, startingMoney: 0, baseDifficulty: 1, minimumSecurity: 1,
        startingSecurity: 1, requiredHackingLevel: 1, growthParameter: 0, maximumRam: 8);

    public static List<FileChange> Record(VirtualFileSystem files)
    {
        var changes = new List<FileChange>();
        files.Changed += changes.Add;
        return changes;
    }
}