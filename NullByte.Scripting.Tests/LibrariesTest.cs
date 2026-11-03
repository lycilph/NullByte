namespace NullByte.Scripting.Tests;

// -- Libraries ------------------------------------------------------------

public class LibrariesTest : IDisposable
{
    private readonly string test_path = Path.Combine(Path.GetTempPath(), "nullbyte-script-tests", Guid.NewGuid().ToString("N"));
    private readonly string base_path;

    public LibrariesTest()
    {
        base_path = Path.Combine(test_path, "scripts");
        Directory.CreateDirectory(ScriptPersistence.LibFolder(base_path));
    }

    public void Dispose()
    {
        try { Directory.Delete(test_path, true); } catch (IOException) { }
    }

    [Fact]
    public async Task Library_HeaderChainsLibrarySource()
    {
        File.WriteAllText(ScriptPersistence.LibPath("mathlib", base_path),
            "int DoubleIt(int x) => x * 2;\n");

        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session, base_path);
        var script = await scripting.StartAsync("user",
            "// uses: mathlib\nnb.Log(\"doubled=\" + DoubleIt(21));");
        await Helper.TickAsync(session, scripting);

        Assert.Contains(script.Output, l => l.Contains("doubled=42"));
    }

    [Fact]
    public async Task Library_Missing_ThrowsWithLibraryName()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session, base_path);

        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("user", "// uses: nosuchlib\nns.Log(\"hi\");"));

        Assert.Contains("nosuchlib", ex.Message);
        Assert.Empty(scripting.Scripts);
    }

    [Fact]
    public async Task Library_InvalidName_Throws()
    {
        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session, base_path);

        // path traversal must die at the name check, before any file IO
        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("user", "// uses: ../evil\nns.Log(\"hi\");"));

        Assert.Contains("Invalid library name", ex.Message);
    }

    [Fact]
    public async Task Library_BannedPattern_Throws()
    {
        File.WriteAllText(ScriptPersistence.LibPath("badlib", base_path),
            "using System.IO;\n");

        var session = Helper.NewSession();
        var scripting = new ScriptingSystem(session, base_path);

        var ex = await Assert.ThrowsAsync<ScriptCompileException>(
            () => scripting.StartAsync("user", "// uses: badlib\nnb.Log(\"hi\");"));

        Assert.Contains("BANNED", ex.Message); // the LIB failed the guard, not just the script
    }
}