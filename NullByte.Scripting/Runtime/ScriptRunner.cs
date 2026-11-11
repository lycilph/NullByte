using System.Reflection;
using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;

namespace NullByte.Scripting.Runtime;

/// <summary>
/// Loads a compiled script into its own collectible context, runs it and unloads it again.
/// </summary>
public sealed class ScriptRunner(ScriptAssemblyLoader assemblyLoader)
{
    // Used by the unload tests.
    internal Action<ScriptLoadContext>? LoadContextCreated { get; set; }

    public async Task RunAsync(CompiledScript script, INullByteApi api, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(api);

        var loaded = assemblyLoader.Load(script);
        LoadContextCreated?.Invoke(loaded.LoadContext);

        try
        {
            // Separate method: the script instance only lives in that method's
            // state machine, so nothing here keeps the assembly alive after it finishes.
            await RunInstanceAsync(loaded.Assembly, api, args.ToArray());
        }
        finally
        {
            assemblyLoader.Unload(loaded);
        }
    }

    private static Task RunInstanceAsync(Assembly assembly, INullByteApi api, string[] args)
    {
        var type = assembly.GetType(ScriptSourceGenerator.TypeName)
            ?? throw new InvalidOperationException("The compiled script does not contain a script class.");

        var instance = (IScript)Activator.CreateInstance(type)!;

        return instance.Run(api, args);
    }
}