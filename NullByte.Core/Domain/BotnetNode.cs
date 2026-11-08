namespace NullByte.Core.Domain;

/// <summary>
/// A single node of the player's botnet.
/// </summary>
/// <remarks>
/// A node only holds its stats. What it produces and what upgrades cost are
/// decided by <see cref="Formulas.BotnetFormulas"/>, and the caps on its
/// stats are enforced by <see cref="Services.BotnetService"/>.
/// </remarks>
public sealed class BotnetNode
{
    public BotnetNode(int level = 1, int ram = 1, int cores = 1)
    {
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be at least one.");
        if (ram < 1 || !int.IsPow2(ram))
            throw new ArgumentOutOfRangeException(nameof(ram), "RAM must be a power of two.");
        if (cores < 1)
            throw new ArgumentOutOfRangeException(nameof(cores), "Cores must be at least one.");

        Level = level;
        Ram = ram;
        Cores = cores;
    }

    /// <summary>
    /// Gets the node's level.
    /// </summary>
    public int Level { get; private set; }

    /// <summary>
    /// Gets the node's RAM. Always a power of two.
    /// </summary>
    public int Ram { get; private set; }

    /// <summary>
    /// Gets the node's number of cores.
    /// </summary>
    public int Cores { get; private set; }

    internal void ApplyUpgrade(BotnetUpgradeKind kind)
    {
        switch (kind)
        {
            case BotnetUpgradeKind.Level:
                Level++;
                break;
            case BotnetUpgradeKind.Ram:
                Ram *= 2;
                break;
            case BotnetUpgradeKind.Cores:
                Cores++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown botnet upgrade.");
        }
    }
}