namespace NullByte.Core.Domain;

/// <summary>
/// The player's botnet: the nodes that earn passive income.
/// </summary>
public sealed class Botnet
{
    private readonly List<BotnetNode> nodes = [];

    /// <summary>
    /// Gets the botnet's nodes, in the order they were bought.
    /// </summary>
    public IReadOnlyList<BotnetNode> Nodes => nodes;

    /// <summary>
    /// Gets the number of nodes.
    /// </summary>
    public int NodeCount => nodes.Count;

    /// <summary>
    /// Gets the total money the botnet has produced so far.
    /// </summary>
    public double TotalEarned { get; private set; }

    internal BotnetNode AddNode()
    {
        var node = new BotnetNode();
        nodes.Add(node);

        return node;
    }

    internal void RecordEarnings(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Earnings must be a finite, non-negative value.");

        TotalEarned += amount;
    }
}