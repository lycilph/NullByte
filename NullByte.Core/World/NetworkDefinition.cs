namespace NullByte.Core.World;

/// <summary>
/// Describes the initial network loaded from world data.
/// </summary>
public sealed record NetworkDefinition
{
    public required IReadOnlyList<ServerDefinition> Servers { get; init; }
    public required IReadOnlyList<ConnectionDefinition> Connections { get; init; }
}

/// <summary>
/// Describes a server in the initial network.
/// </summary>
public sealed record ServerDefinition
{
    public required string Hostname { get; init; }
    public required double MaximumMoney { get; init; }
    public required double StartingMoney { get; init; }
    public required double MinimumSecurity { get; init; }
    public required double StartingSecurity { get; init; }
    public required int RequiredHackingLevel { get; init; }
    public required double GrowthParameter { get; init; }
    public required double MaximumRam { get; init; }
    public bool HasRootAccess { get; init; }
}

/// <summary>
/// Describes a bidirectional connection between two servers.
/// </summary>
public sealed record ConnectionDefinition
{
    public required string From { get; init; }
    public required string To { get; init; }
}