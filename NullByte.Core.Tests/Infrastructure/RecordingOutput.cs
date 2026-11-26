using NullByte.Core.Services;

namespace NullByte.Core.Tests.Infrastructure;

/// <summary>Collects everything a script or service prints, so tests can assert on the lines.</summary>
internal sealed class RecordingOutput : IGameOutput
{
    private readonly List<string> lines = [];

    public IReadOnlyList<string> Lines => lines;

    public void WriteLine(string message) => lines.Add(message);

    public void WriteLine() => lines.Add(string.Empty);
}
