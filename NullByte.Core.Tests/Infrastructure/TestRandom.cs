using NSubstitute;
using NullByte.Core.Randomness;

namespace NullByte.Core.Tests.Infrastructure;

internal static class TestRandom
{
    /// <summary>
    /// A random source that always rolls <paramref name="value"/>. Use <see cref="ScriptedRandom"/> instead when the
    /// test needs a sequence of rolls.
    /// </summary>
    public static IRandomSource Always(double value)
    {
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(value);

        return random;
    }
}
