namespace NullByte.Shell.Persistence;

public interface ISaveStore
{
    Task WriteAsync(string slot, string json, CancellationToken cancellationToken = default);
    Task<string?> ReadAsync(string slot, CancellationToken cancellationToken = default);   // null = missing
    Task DeleteAsync(string slot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed record SaveSlotInfo(string Name, DateTimeOffset SavedAtUtc);

public static class SaveSlot
{
    public const int MaxLength = 32;

    public static bool IsValidName(string? name)
        => name is { Length: > 0 and <= MaxLength } && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}