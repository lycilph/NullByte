using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;

namespace NullByte.Scripting.Tests.Loading;

public sealed class ScriptAssemblyLoaderTests
{
    private readonly ScriptCompiler compiler = new();
    private readonly ScriptAssemblyLoader loader = new();

    [Fact]
    public void Load_LoadsCompiledAssembly()
    {
        var compiled = Compile(
            """
            nb.Print("Hello");
            """);

        var loaded = loader.Load(compiled);

        try
        {
            Assert.NotNull(loaded.Assembly);
        }
        finally
        {
            loader.Unload(loaded);
        }
    }

    [Fact]
    public void Load_UsesCollectibleContext()
    {
        var compiled = Compile(
            """
            nb.Print("Hello");
            """);

        var loaded = loader.Load(compiled);

        try
        {
            Assert.True(loaded.LoadContext.IsCollectible);
        }
        finally
        {
            loader.Unload(loaded);
        }
    }

    [Fact]
    public void Load_WithInvalidAssembly_Throws()
    {
        var compiled = new CompiledScript([1, 2, 3, 4], []);

        Assert.Throws<BadImageFormatException>(() => loader.Load(compiled));
    }

    private CompiledScript Compile(string source)
    {
        var result = compiler.Compile(source, "test.cs");

        Assert.True(
            result.Succeeded,
            string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: " + diagnostic.Message)));

        return Assert.IsType<CompiledScript>(result.Script);
    }
}