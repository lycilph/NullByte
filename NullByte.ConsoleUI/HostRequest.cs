namespace NullByte.ConsoleUI;

/// <summary>
/// An operation that must be performed by the host after the current
/// command has returned from the game thread.
/// </summary>
public abstract record HostRequest
{
    private HostRequest() { }

    public sealed record Shutdown : HostRequest;

    public sealed record Load : HostRequest
    {
        public Load(string slot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(slot);
            Slot = slot;
        }

        public string Slot { get; }
    }

    public sealed record Reset : HostRequest;
}