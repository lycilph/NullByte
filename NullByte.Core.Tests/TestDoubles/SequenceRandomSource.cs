using NullByte.Core.Randomness;

namespace NullByte.Core.Tests.TestDoubles;

internal sealed class SequenceRandomSource : IRandomSource
{
    private readonly Queue<double> values;

    public SequenceRandomSource(params double[] values)
    {
        if (values.Length == 0)
        {
            throw new ArgumentException(
                "At least one value must be provided.",
                nameof(values));
        }

        if (values.Any(value =>
                !double.IsFinite(value) ||
                value < 0 ||
                value >= 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                "Values must be between 0.0 inclusive and 1.0 exclusive.");
        }

        this.values = new Queue<double>(values);
    }

    public double NextDouble()
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No random values remain in the sequence.");
        }

        return values.Dequeue();
    }
}