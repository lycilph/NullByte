using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Scripting.Runtime;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Runtime;

public sealed class ScriptProcessManagerTests
{
    private readonly ScriptProcessManager manager = new();

    private ScriptProcess Create(string filename = "hack.cx", ServerId? host = null, string[]? args = null)
        => manager.Create(filename, host ?? ServerId.New(), args ?? Array.Empty<string>(), 1, 1);

    [Fact]
    public void Create_AssignsIncreasingPids()
    {
        var first = Create("hack.cx");
        var second = Create("grow.cx", args: ["n00dles"]);

        Assert.Equal(1, first.Pid);
        Assert.Equal(2, second.Pid);
    }

    [Fact]
    public void Create_StoresTheProcessMetadata()
    {
        var process = Create("hack.cx", args: ["n00dles", "extra"]);

        Assert.Equal("hack.cx", process.Filename);
        Assert.True(process.HostServerId.IsValid);
        Assert.Equal(["n00dles", "extra"], process.Args);
        Assert.Equal(ScriptProcessState.Running, process.State);
    }

    [Fact]
    public void GetForHost_FiltersByStableServerIdentity()
    {
        var hostId = ServerId.New();

        var hosted = Create("hosted.cx", hostId);
        Create("other.cx", ServerId.New());

        Assert.Same(hosted, Assert.Single(manager.GetForHost(hostId)));
    }

    [Fact]
    public void RenameHostServer_DoesNotChangeTheRunningProcessHostIdentity()
    {
        var network = new Network();
        var cloud = TestServers.Create("cloud-old", isCloudServer: true);
        network.AddServer(cloud);

        var process = Create("running.cx", cloud.Id);

        network.RenameServer(cloud.Id, "cloud-new");

        Assert.Equal(cloud.Id, process.HostServerId);
        Assert.Same(process, Assert.Single(manager.GetForHost(cloud.Id)));
        Assert.Same(cloud, network.GetServer("cloud-new"));
    }

    [Fact]
    public void TryGet_WithAKnownPid_ReturnsTheProcess()
    {
        var created = Create();

        Assert.True(manager.TryGet(created.Pid, out var found));
        Assert.Same(created, found);
    }

    [Fact]
    public void UnknownPid_IsNotFoundAndCannotBeKilled()
    {
        Assert.False(manager.TryGet(123, out _));
        Assert.False(manager.Kill(123));
    }

    [Fact]
    public void Kill_CancelsARunningProcess()
    {
        var process = Create();

        Assert.True(manager.Kill(process.Pid));
        Assert.Equal(ScriptProcessState.Killed, process.State);
        Assert.True(process.Cancellation.IsCancellationRequested);
    }

    [Fact]
    public void Kill_DoesNotKillAnAlreadyCompletedProcess()
    {
        var process = Create();
        process.State = ScriptProcessState.Completed;

        Assert.False(manager.Kill(process.Pid));
        Assert.Equal(ScriptProcessState.Completed, process.State);
    }

    [Fact]
    public void KillAll_CancelsAllRunningProcesses()
    {
        var first = Create("a.cx");
        var second = Create("b.cx");

        Assert.Equal(2, manager.KillAll());

        Assert.Equal(ScriptProcessState.Killed, first.State);
        Assert.Equal(ScriptProcessState.Killed, second.State);
        Assert.True(first.Cancellation.IsCancellationRequested);
        Assert.True(second.Cancellation.IsCancellationRequested);
    }

    [Fact]
    public void RemoveCompleted_RemovesFinishedProcesses_AndKeepsTheOthers()
    {
        var finished = Create("done.cx");
        finished.State = ScriptProcessState.Completed;
        finished.Execution = Task.CompletedTask;

        var running = Create("running.cx");

        manager.RemoveCompleted();

        Assert.False(manager.TryGet(finished.Pid, out _));
        Assert.True(manager.TryGet(running.Pid, out _));
    }
}
