namespace NullByte.Core.Persistence;

/// <summary>
/// The save is not valid JSON, is from a newer version, or holds data the game would reject.
/// The message is written for the player.
/// </summary>
public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string message) : base(message) {}

    public SaveFormatException(string message, Exception innerException) : base(message, innerException) {}
}
