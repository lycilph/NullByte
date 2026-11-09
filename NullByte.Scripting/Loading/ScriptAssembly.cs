using System.Reflection;

namespace NullByte.Scripting.Loading;

/// <summary>
/// Represents a compiled script loaded into a collectible assembly context.
/// </summary>
public sealed class ScriptAssembly
{
    internal ScriptAssembly(ScriptLoadContext loadContext, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(loadContext);
        ArgumentNullException.ThrowIfNull(assembly);

        LoadContext = loadContext;
        Assembly = assembly;
    }

    public Assembly Assembly { get; }

    internal ScriptLoadContext LoadContext { get; }
}