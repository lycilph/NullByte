namespace NullByte.Core.Narrative;

/// <summary>Whether a discovery is optional story material or part of the required narrative arc.</summary>
public enum NarrativeDiscoveryRequirement
{
    Optional,
    Required
}

/// <summary>A narrative discovery associated with a file in a server's virtual filesystem.</summary>
public sealed record NarrativeDiscovery(
    string Id,
    string Hostname,
    string Path,
    NarrativeDiscoveryRequirement Requirement);
