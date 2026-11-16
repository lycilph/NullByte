namespace NullByte.Core.Scripting;

public interface INullByteApiFactory
{
    INullByteApi Create(IScriptOperations scriptOperations, string processHostname, int threads, CancellationToken cancellationToken);
}