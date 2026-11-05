namespace NullByte.Core.Randomness;

/// <summary>
/// Provides random values for game simulation logic.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Returns a random floating-point value greater than or equal
    /// to 0.0 and less than 1.0.
    /// </summary>
    double NextDouble();
}