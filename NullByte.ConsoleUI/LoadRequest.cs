namespace NullByte.ConsoleUI;

public sealed class LoadRequest
{
    private string? slot;

    public void Request(string slotName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotName);
        slot = slotName;
    }

    /// <summary>Returns the requested slot, if any, and clears the request.</summary>
    public string? Take()
    {
        var requested = slot;
        slot = null;
        return requested;
    }
}