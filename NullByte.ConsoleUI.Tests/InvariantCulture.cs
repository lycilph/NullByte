using System.Globalization;

namespace NullByte.ConsoleUI.Tests;

/// <summary>
/// Makes the current thread use the invariant culture until disposed, so that tests which
/// check formatted numbers ("$1,234.50") give the same result on every machine.
/// </summary>
internal sealed class InvariantCulture : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public InvariantCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = previous;
    }
}