namespace NullByte.Core.Domain;

/// <summary>
/// Immutable identity for a server, independent of its player-facing hostname.
/// </summary>
public readonly record struct ServerId(Guid Value)
{
    /// <summary>
    /// Creates a new unique server identity.
    /// </summary>
    public static ServerId New() => new(Guid.NewGuid());

    /// <summary>
    /// Indicates whether this ID contains a valid, non-empty GUID.
    /// </summary>
    public bool IsValid => Value != Guid.Empty;

    public static ServerId Parse(string value)
    {
        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            throw new FormatException($"'{value}' is not a valid server ID.");

        return new ServerId(guid);
    }

    public override string ToString() => Value.ToString("D");
}
