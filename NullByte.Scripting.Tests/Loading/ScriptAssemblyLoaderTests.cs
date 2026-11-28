using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Loading;

public sealed class ScriptAssemblyLoaderTests
{
    private readonly ScriptAssemblyLoader loader = new();

    [Fact]
    public void Load_ReturnsTheAssemblyInACollectibleContext()
    {
        var loaded = loader.Load(TestScripts.CompileOrFail("nb.Print(\"Hello\");"));

        try
        {
            Assert.NotNull(loaded.Assembly);
            Assert.True(loaded.LoadContext.IsCollectible);
        }
        finally
        {
            loader.Unload(loaded);
        }
    }

    [Fact]
    public void Load_WithAnInvalidAssembly_Throws()
    {
        var compiled = new CompiledScript([1, 2, 3, 4], [], 1.0);

        Assert.Throws<BadImageFormatException>(() => loader.Load(compiled));
    }
}
