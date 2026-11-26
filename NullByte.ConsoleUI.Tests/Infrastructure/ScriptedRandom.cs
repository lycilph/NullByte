using NullByte.Core.Randomness;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

/// <summary>
/// A random source whose rolls the test decides: queued values come out first, then <see cref="Default"/>.
/// A roll succeeds when it is below the chance, so 0 (the default) succeeds whenever the chance is above zero.
/// </summary>
internal sealed class ScriptedRandom : IRandomSource
{
    private readonly Queue<double> queued = new();

    public double Default { get; set; }

    public void Enqueue(params double[] values)
    {
        foreach (var value in values)
            queued.Enqueue(value);
    }

    public double NextDouble() => queued.Count > 0 ? queued.Dequeue() : Default;
}