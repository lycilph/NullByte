using System.ComponentModel.DataAnnotations;

namespace NullByte.Core.Configuration;

public sealed class GameConfig
{
    [Range(0.01, 100.0)]
    public double TimeMultiplier { get; init; } = 1.0;
}