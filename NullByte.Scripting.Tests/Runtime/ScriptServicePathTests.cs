using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Runtime;

/// <summary>"/tools/a.cx" and "tools/a.cx" are the same script; bad paths are reported, not thrown.</summary>
public sealed class ScriptServicePathTests
{
    private readonly ScriptWorld world = ScriptTestSetup.CreateWorld();

    [Fact]
    public async Task Start_WithAnInvalidPath_ReportsAFailureInsteadOfThrowing()
    {
        var result = await world.StartAsync("a//b");

        Assert.False(result.Succeeded);
        Assert.StartsWith("Invalid file name 'a//b': ", result.FailureMessage);
    }

    [Fact]
    public async Task Start_WithADirectoryOrAMissingFile_NamesTheNormalizedPath()
    {
        world.WriteScript("tools/a.cx", "nb.GetHackingLevel();");

        Assert.Equal("tools is a directory.", (await world.StartAsync("/tools")).FailureMessage);
        Assert.Equal("File not found: tools/nope.cx", (await world.StartAsync("/tools/nope.cx")).FailureMessage);
    }

    [Fact]
    public void GetScriptRam_WithDifferentlySpelledPaths_FindsTheScript()
    {
        world.WriteScript("tools/a.cx", "nb.Hack();");

        var ram = world.Service.GetScriptRam("tools/a.cx", "home");

        Assert.True(ram > 0);
        Assert.Equal(ram, world.Service.GetScriptRam("/tools/a.cx", "home"));
        Assert.Equal(0.0, world.Service.GetScriptRam("tools/nope.cx", "home"));
        Assert.Equal(0.0, world.Service.GetScriptRam("a//b", "home"));
    }

    [Fact]
    public void ScriptRunning_WithDifferentlySpelledPaths_MatchesTheSameScript()
    {
        world.Processes.Create("tools/a.cx", world.Home.Id, [], 1, 0);

        Assert.True(world.Service.ScriptRunning("tools/a.cx", "home"));
        Assert.True(world.Service.ScriptRunning("/tools/a.cx", "home"));
        Assert.False(world.Service.ScriptRunning("TOOLS/a.cx", "home"));   // paths are case-sensitive
        Assert.False(world.Service.ScriptRunning("tools/b.cx", "home"));
        Assert.False(world.Service.ScriptRunning("a//b", "home"));
    }
}
