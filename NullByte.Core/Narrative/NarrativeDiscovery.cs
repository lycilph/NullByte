using NullByte.Core.Domain;

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

/// <summary>Maps player-readable virtual files to stable narrative discovery IDs.</summary>
public sealed class NarrativeDiscoveryCatalog
{
    private readonly IReadOnlyDictionary<string, NarrativeDiscovery> discoveriesByFile;

    public NarrativeDiscoveryCatalog(IEnumerable<NarrativeDiscovery> discoveries)
    {
        ArgumentNullException.ThrowIfNull(discoveries);

        var byId = new HashSet<string>(StringComparer.Ordinal);
        var byFile = new Dictionary<string, NarrativeDiscovery>(StringComparer.Ordinal);

        foreach (var discovery in discoveries)
        {
            ArgumentNullException.ThrowIfNull(discovery);
            ArgumentException.ThrowIfNullOrWhiteSpace(discovery.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(discovery.Hostname);
            if (discovery.Requirement is not NarrativeDiscoveryRequirement.Optional and not NarrativeDiscoveryRequirement.Required)
                throw new ArgumentOutOfRangeException(nameof(discoveries), discovery.Requirement, "Unknown narrative discovery requirement.");

            var normalized = discovery with { Path = VirtualPath.NormalizeFile(discovery.Path) };
            if (!byId.Add(normalized.Id))
                throw new ArgumentException($"Duplicate narrative discovery ID '{normalized.Id}'.", nameof(discoveries));

            var key = GetKey(normalized.Hostname, normalized.Path);
            if (!byFile.TryAdd(key, normalized))
                throw new ArgumentException($"More than one narrative discovery is registered for '{normalized.Hostname}:{normalized.Path}'.", nameof(discoveries));
        }

        discoveriesByFile = byFile;
    }

    /// <summary>The shipped discovery catalog. Later narrative milestones add entries here.</summary>
    public static NarrativeDiscoveryCatalog Default { get; } = new([]);

    public bool TryGet(string hostname, string path, out NarrativeDiscovery? discovery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        var normalizedPath = VirtualPath.NormalizeFile(path);
        return discoveriesByFile.TryGetValue(GetKey(hostname, normalizedPath), out discovery);
    }

    private static string GetKey(string hostname, string path)
        => hostname.ToUpperInvariant() + "\\0" + path;
}
