using Microsoft.Extensions.Time.Testing;
using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Infrastructure;

/// <summary>Builds a virtual file system over a small network with a controllable clock.</summary>
internal static class TestFileSystem
{
    public static readonly DateTimeOffset Start = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    public static (VirtualFileSystem Files, FakeTimeProvider Clock) Create()
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));

        var clock = new FakeTimeProvider(Start);
        return (new VirtualFileSystem(network, clock), clock);
    }

    public static Server NewServer(string hostname) => TestServers.Create(hostname, baseDifficulty: 1);

    public static List<FileChange> Record(VirtualFileSystem files)
    {
        var changes = new List<FileChange>();
        files.Changed += changes.Add;
        return changes;
    }
}
