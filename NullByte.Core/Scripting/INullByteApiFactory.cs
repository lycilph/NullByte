using NullByte.Core.Domain;

namespace NullByte.Core.Scripting;

public interface INullByteApiFactory
{
    INullByteApi Create(IScriptOperations scriptOperations, ServerId processHostServerId, int threads, CancellationToken cancellationToken);
}