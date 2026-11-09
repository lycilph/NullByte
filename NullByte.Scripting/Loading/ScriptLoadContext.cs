using System.Reflection;
using System.Runtime.Loader;

namespace NullByte.Scripting.Loading;

/// <summary>
/// Provides an isolated, collectible load context for a compiled player
/// script.
/// </summary>
public sealed class ScriptLoadContext : AssemblyLoadContext
{
    public ScriptLoadContext() : base(isCollectible: true) {}

    protected override Assembly? Load(AssemblyName assemblyName) => null;
}