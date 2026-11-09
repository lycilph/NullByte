using NullByte.Core.Scripting;

namespace NullByte.Scripting.Runtime;

public static class ScriptRuntime
{
    private static INullByteApi? currentApi;

    public static INullByteApi CurrentApi =>
        currentApi ?? throw new InvalidOperationException("No NullByte API is bound to the current script.");

    internal static void Bind(INullByteApi api)
    {
        ArgumentNullException.ThrowIfNull(api);

        if (currentApi is not null)
            throw new InvalidOperationException("A NullByte API is already bound.");

        currentApi = api;
    }

    internal static void Unbind()
    {
        currentApi = null;
    }
}