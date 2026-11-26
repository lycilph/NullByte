using NullByte.ConsoleUI.Tests.Infrastructure;

namespace NullByte.ConsoleUI.Tests;

public class ScriptCommandsPathTests
{
    [Fact]
    public async Task Mem_finds_the_script_however_its_path_is_spelled()
    {
        var game = ConsoleTestSetup.CreateHarness();
        game.Files.WriteFile("home", "tools/a.cx", "nb.GetHackingLevel();");
        await game.RunAndCaptureAsync("cd tools");

        var relative = await game.RunAndCaptureAsync("mem a.cx");

        var line = Assert.Single(relative);
        Assert.StartsWith("This script requires", line);
        Assert.DoesNotContain("requires 0.00GB", line);   // 0 would mean it was not found

        Assert.Equal(relative, await game.RunAndCaptureAsync("mem /tools/a.cx"));
        Assert.Equal(relative, await game.RunAndCaptureAsync("mem ../tools/a.cx"));

        await game.RunAndCaptureAsync("cd /");
        Assert.Equal(relative, await game.RunAndCaptureAsync("mem tools/a.cx"));
    }

    [Fact]
    public async Task Run_reports_missing_files_and_directories_by_their_full_path()
    {
        var game = ConsoleTestSetup.CreateHarness();
        game.Files.CreateDirectory("home", "tools/sub");
        await game.RunAndCaptureAsync("cd tools");

        Assert.Equal(
            new[] { "Executing tools/nope.cx...", "File not found: tools/nope.cx" },
            await game.RunAndCaptureAsync("run nope.cx"));

        Assert.Equal(
            new[] { "Executing tools/sub...", "tools/sub is a directory." },
            await game.RunAndCaptureAsync("run sub"));
    }

    [Fact]
    public async Task Run_and_mem_refuse_a_bad_path_before_anything_else()
    {
        var game = ConsoleTestSetup.CreateHarness();
        await game.RunAndCaptureAsync("mkdir tools");   // for the cd below, not needed: just keeps the game non-empty
        await game.RunAndCaptureAsync("cd tools");

        Assert.StartsWith("Invalid file name: Path segments must be simple names", Assert.Single(await game.RunAndCaptureAsync("run a//b")));
        Assert.Equal(new[] { "Invalid file name: A file path cannot be empty." }, await game.RunAndCaptureAsync("run .."));
        Assert.Equal(new[] { "Invalid file name: A file path cannot be empty." }, await game.RunAndCaptureAsync("mem .."));
    }
}