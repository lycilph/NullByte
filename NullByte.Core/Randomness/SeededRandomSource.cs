namespace NullByte.Core.Randomness;

/// <summary>
/// Provides deterministic pseudo-random values from a fixed seed.
/// </summary>
public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private readonly Random random = new Random(seed);

    public double NextDouble() => random.NextDouble();
}