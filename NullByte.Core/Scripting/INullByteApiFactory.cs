namespace NullByte.Core.Scripting;

public interface INullByteApiFactory
{
    INullByteApi Create(IScriptOperations scriptOperations, int threads, CancellationToken cancellationToken);
}