using System.Reflection;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;

namespace NullByte.Scripting.Runtime;

/// <summary>
/// Loads and executes compiled player scripts.
/// </summary>
public sealed class ScriptRunner
{
    internal Action<ScriptLoadContext>? LoadContextCreated { get; set; }

    private readonly ScriptAssemblyLoader assemblyLoader;

    public ScriptRunner(ScriptAssemblyLoader assemblyLoader)
    {
        ArgumentNullException.ThrowIfNull(assemblyLoader);

        this.assemblyLoader = assemblyLoader;
    }

    /// <summary>
    /// Executes a compiled player script with the supplied API and command-line
    /// arguments.
    /// </summary>
    public async Task RunAsync(CompiledScript script, INullByteApi api, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(args);

        var loadedScript = assemblyLoader.Load(script);

        // Used in the tests
        LoadContextCreated?.Invoke(loadedScript.LoadContext);
        
        var apiWasBound = false;

        try
        {
            ScriptRuntime.Bind(api);
            apiWasBound = true;

            var scriptArgs = args.ToArray();

            await InvokeTopLevelProgramAsync(loadedScript.Assembly, scriptArgs);
        }
        finally
        {
            if (apiWasBound)
                ScriptRuntime.Unbind();

            assemblyLoader.Unload(loadedScript);
        }
    }

    private static async Task InvokeTopLevelProgramAsync(Assembly assembly, string[] args)
    {
        var entryPoint = assembly.EntryPoint
            ?? throw new InvalidOperationException("The compiled script does not have an entry point.");

        var method = FindTopLevelImplementation(entryPoint);

        object? result;

        try
        {
            result = InvokeMethod(method, args);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            RethrowInnerException(exception.InnerException);
            throw;
        }

        if (result is Task task)
        {
            await task;
        }
    }

    private static MethodInfo FindTopLevelImplementation(MethodInfo entryPoint)
    {
        if (typeof(Task).IsAssignableFrom(entryPoint.ReturnType))
        {
            return entryPoint;
        }

        var declaringType = entryPoint.DeclaringType
            ?? throw new InvalidOperationException("The script entry point does not have a declaring type.");

        var asyncImplementation = declaringType
            .GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Where(method => method != entryPoint)
            .Where(AcceptsStringArray)
            .SingleOrDefault(method => typeof(Task).IsAssignableFrom(method.ReturnType));

        return asyncImplementation ?? entryPoint;
    }

    private static bool AcceptsStringArray(MethodInfo method)
    {
        var parameters = method.GetParameters();

        return parameters.Length == 1 &&
               parameters[0].ParameterType == typeof(string[]);
    }

    private static object? InvokeMethod(MethodInfo method, string[] args)
    {
        var parameters = method.GetParameters();

        if (parameters.Length == 0)
        {
            return method.Invoke(obj: null, parameters: null);
        }

        if (parameters.Length == 1 &&
            parameters[0].ParameterType == typeof(string[]))
        {
            return method.Invoke(obj: null, parameters: [args]);
        }

        throw new InvalidOperationException($"The script entry method '{method.Name}' has an unsupported signature.");
    }

    private static void RethrowInnerException(Exception exception)
    {
        System.Runtime.ExceptionServices
            .ExceptionDispatchInfo
            .Capture(exception)
            .Throw();
    }
}