using NullByte.Scripting.Runtime;

namespace NullByte.Scripting.Tests.Runtime;

public sealed class ScriptProcessManagerTests
{
    [Fact]
    public void Create_AssignsIncreasingPids()
    {
        var manager = new ScriptProcessManager();

        var first = manager.Create(
            "hack.cx",
            "home",
            [], 1, 1);

        var second = manager.Create(
            "grow.cx",
            "home",
            ["n00dles"], 1, 1);

        Assert.Equal(1, first.Pid);
        Assert.Equal(2, second.Pid);
    }

    [Fact]
    public void Create_StoresProcessMetadata()
    {
        var manager = new ScriptProcessManager();

        var process = manager.Create(
            "hack.cx",
            "home",
            ["n00dles", "extra"], 1, 1);

        Assert.Equal("hack.cx", process.FileName);
        Assert.Equal("home", process.Hostname);
        Assert.Equal(["n00dles", "extra"], process.Args);
        Assert.Equal(ScriptProcessState.Running, process.State);
    }

    [Fact]
    public void TryGet_ReturnsProcess()
    {
        var manager = new ScriptProcessManager();

        var created = manager.Create(
            "hack.cx",
            "home",
            [], 1, 1);

        Assert.True(manager.TryGet(created.Pid, out var found));
        Assert.Same(created, found);
    }

    [Fact]
    public void TryGet_UnknownPidReturnsFalse()
    {
        var manager = new ScriptProcessManager();

        Assert.False(manager.TryGet(123, out _));
    }

    [Fact]
    public void Kill_CancelsRunningProcess()
    {
        var manager = new ScriptProcessManager();

        var process = manager.Create(
            "hack.cx",
            "home",
            [], 1, 1);

        Assert.True(manager.Kill(process.Pid));
        Assert.Equal(ScriptProcessState.Killed, process.State);
        Assert.True(process.Cancellation.IsCancellationRequested);
    }

    [Fact]
    public void Kill_UnknownPidReturnsFalse()
    {
        var manager = new ScriptProcessManager();

        Assert.False(manager.Kill(123));
    }

    [Fact]
    public void KillAll_CancelsAllRunningProcesses()
    {
        var manager = new ScriptProcessManager();

        var first = manager.Create("a.cx", "home", [], 1, 1);
        var second = manager.Create("b.cx", "home", [], 1, 1);

        var killed = manager.KillAll();

        Assert.Equal(2, killed);

        Assert.Equal(ScriptProcessState.Killed, first.State);
        Assert.Equal(ScriptProcessState.Killed, second.State);

        Assert.True(first.Cancellation.IsCancellationRequested);
        Assert.True(second.Cancellation.IsCancellationRequested);
    }

    [Fact]
    public void Kill_DoesNotKillAlreadyCompletedProcess()
    {
        var manager = new ScriptProcessManager();

        var process = manager.Create(
            "hack.cx",
            "home",
            [], 1, 1);

        process.State = ScriptProcessState.Completed;

        Assert.False(manager.Kill(process.Pid));
        Assert.Equal(ScriptProcessState.Completed, process.State);
    }
}