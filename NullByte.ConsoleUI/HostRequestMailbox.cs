namespace NullByte.ConsoleUI;

/// <summary>
/// Carries a single lifecycle request to the game host.
///
/// A request remains pending until the host takes it. Only one request
/// can be pending at a time.
/// </summary>
public sealed class HostRequestMailbox
{
    private HostRequest? pending;

    /// <summary>
    /// Attempts to submit a host-level request.
    /// </summary>
    /// <returns>
    /// True if the request was accepted; false if another request is already pending.
    /// </returns>
    public bool TryRequest(HostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Interlocked.CompareExchange(ref pending, request, null) is null;
    }

    /// <summary>
    /// Submits a host-level request.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Another request is already pending.
    /// </exception>
    public void Request(HostRequest request)
    {
        if (!TryRequest(request))
            throw new InvalidOperationException("A host request is already pending.");
    }

    /// <summary>
    /// Atomically returns and removes the pending request.
    /// </summary>
    public HostRequest? Take()
    {
        return Interlocked.Exchange(ref pending, null);
    }
}