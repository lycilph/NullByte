using NullByte.Core.Randomness;

namespace NullByte.Core.Tests.Randomness;

public sealed class SeededRandomSourceTests
{
    [Fact]
    public void NextDouble_ReturnsValueInExpectedRange()
    {
        var random = new SeededRandomSource(1337);

        for (var i = 0; i < 1_000; i++)
        {
            var value = random.NextDouble();

            Assert.InRange(
                value,
                0.0,
                double.BitDecrement(1.0));
        }
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var first = new SeededRandomSource(1337);
        var second = new SeededRandomSource(1337);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(
                first.NextDouble(),
                second.NextDouble());
        }
    }
}