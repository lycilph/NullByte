namespace NullByte.Scripting.Tests;

// -- Libraries ------------------------------------------------------------

public class LibrariesTest
{
    /// <summary>Sets up an isolated cwd so lib/script file IO stays in the test.</summary>
    private static (string Dir, string OldCwd) TempScriptDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nullbyte-script-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "scripts", "lib"));
        string old = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(dir);
        return (dir, old);
    }

    private static void CleanupScriptDir(string dir, string oldCwd)
    {
        Directory.SetCurrentDirectory(oldCwd);
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    [Fact]
    public async Task Library_HeaderChainsLibrarySource()
    {
        var (dir, old) = TempScriptDir();
        try
        {
            File.WriteAllText(Path.Combine("scripts", "lib", "mathlib.cs"),
                "int DoubleIt(int x) => x * 2;\n");

            var session = Helper.NewSession();
            var scripting = new ScriptingSystem(session);
            var script = await scripting.StartAsync("user",
                "// uses: mathlib\nnb.Log(\"doubled=\" + DoubleIt(21));");
            await Helper.TickAsync(session, scripting);

            Assert.Contains(script.Output, l => l.Contains("doubled=42"));
        }
        finally
        {
            CleanupScriptDir(dir, old);
        }
    }

    [Fact]
    public async Task Library_Missing_ThrowsWithLibraryName()
    {
        var (dir, old) = TempScriptDir();
        try
        {
            var session = Helper.NewSession();
            var scripting = new ScriptingSystem(session);

            var ex = await Assert.ThrowsAsync<ScriptCompileException>(
                () => scripting.StartAsync("user", "// uses: nosuchlib\nns.Log(\"hi\");"));

            Assert.Contains("nosuchlib", ex.Message);
            Assert.Empty(scripting.Scripts);
        }
        finally
        {
            CleanupScriptDir(dir, old);
        }
    }

    [Fact]
    public async Task Library_InvalidName_Throws()
    {
        var (dir, old) = TempScriptDir();
        try
        {
            var session = Helper.NewSession();
            var scripting = new ScriptingSystem(session);

            // path traversal must die at the name check, before any file IO
            var ex = await Assert.ThrowsAsync<ScriptCompileException>(
                () => scripting.StartAsync("user", "// uses: ../evil\nns.Log(\"hi\");"));

            Assert.Contains("Invalid library name", ex.Message);
        }
        finally
        {
            CleanupScriptDir(dir, old);
        }
    }

    [Fact]
    public async Task Library_BannedPattern_Throws()
    {
        var (dir, old) = TempScriptDir();
        try
        {
            File.WriteAllText(Path.Combine("scripts", "lib", "badlib.cs"),
                "using System.IO;\n");

            var session = Helper.NewSession();
            var scripting = new ScriptingSystem(session);

            var ex = await Assert.ThrowsAsync<ScriptCompileException>(
                () => scripting.StartAsync("user", "// uses: badlib\nns.Log(\"hi\");"));

            Assert.Contains("BANNED", ex.Message); // the LIB failed the guard, not just the script
        }
        finally
        {
            CleanupScriptDir(dir, old);
        }
    }
}
