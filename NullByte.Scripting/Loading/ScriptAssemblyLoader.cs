using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Loading;

/// <summary>
/// Loads compiled scripts into collectible assembly load contexts.
/// </summary>
public sealed class ScriptAssemblyLoader
{
    public ScriptAssembly Load(CompiledScript script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var loadContext = new ScriptLoadContext();

        try
        {
            using var assemblyStream = new MemoryStream(script.Assembly, writable: false);
            using var symbolsStream = new MemoryStream(script.Symbols, writable: false);

            var assembly = loadContext.LoadFromStream(assemblyStream, symbolsStream);

            return new ScriptAssembly(loadContext, assembly);
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    public void Unload(ScriptAssembly script)
    {
        ArgumentNullException.ThrowIfNull(script);

        script.LoadContext.Unload();
    }
}