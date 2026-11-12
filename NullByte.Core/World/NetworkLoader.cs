using System.Text.Json;
using NullByte.Core.Domain;

namespace NullByte.Core.World;

/// <summary>
/// Loads a game network from its serialized world definition.
/// </summary>
public sealed class NetworkLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads a network definition from JSON and constructs the network.
    /// </summary>
    public Network Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Network JSON cannot be empty.", nameof(json));

        var definition = JsonSerializer.Deserialize<NetworkDefinition>(json, JsonOptions);
        if (definition is null)
            throw new InvalidDataException("Network JSON did not contain a valid network definition.");

        return Load(definition);
    }

    /// <summary>
    /// Constructs a network from an already deserialized definition.
    /// </summary>
    public Network Load(NetworkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Servers is null)
            throw new InvalidDataException("Network definition must contain a server collection.");
        if (definition.Connections is null)
            throw new InvalidDataException("Network definition must contain a connection collection.");

        var network = new Network();

        foreach (var serverDefinition in definition.Servers)
        {
            var server = CreateServer(serverDefinition);
            network.AddServer(server);
        }

        foreach (var connection in definition.Connections)
        {
            Connect(network, connection);
        }

        return network;
    }

    private static Server CreateServer(ServerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new Server(
            hostname: definition.Hostname,
            maximumMoney: definition.MaximumMoney,
            startingMoney: definition.StartingMoney,
            baseDifficulty: definition.BaseDifficulty,
            minimumSecurity: definition.MinimumSecurity,
            startingSecurity: definition.StartingSecurity,
            requiredHackingLevel: definition.RequiredHackingLevel,
            growthParameter: definition.GrowthParameter,
            maximumRam: definition.MaximumRam,
            hasRootAccess: definition.HasRootAccess);
    }

    private static void Connect(Network network, ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.From))
            throw new InvalidDataException("A network connection must specify a source server.");
        if (string.IsNullOrWhiteSpace(definition.To))
            throw new InvalidDataException("A network connection must specify a destination server.");

        network.Connect(definition.From, definition.To);
    }
}