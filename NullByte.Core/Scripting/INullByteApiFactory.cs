namespace NullByte.Core.Scripting;

public interface INullByteApiFactory
{
    INullByteApi Create(int threads, CancellationToken cancellationToken);
}