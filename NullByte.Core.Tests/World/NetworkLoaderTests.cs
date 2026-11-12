using System.Text.Json;
using NullByte.Core.World;

namespace NullByte.Core.Tests.World;

public sealed class NetworkLoaderTests
{
    [Fact]
    public void Load_FromJson_CreatesServers()
    {
        var loader = new NetworkLoader();

        var network = loader.Load(ValidJson);

        Assert.Equal(2, network.Servers.Count);

        var home = network.GetServer("home");
        var target = network.GetServer("target");

        Assert.Equal("home", home.Hostname);
        Assert.True(home.HasRootAccess);

        Assert.Equal("target", target.Hostname);
        Assert.Equal(100_000, target.MaximumMoney);
        Assert.Equal(50_000, target.Money);
        Assert.Equal(1, target.MinimumSecurity);
        Assert.Equal(2, target.Security);
        Assert.Equal(10, target.RequiredHackingLevel);
        Assert.Equal(20, target.GrowthParameter);
        Assert.Equal(4, target.MaximumRam);
        Assert.False(target.HasRootAccess);
    }

    [Fact]
    public void Load_FromJson_CreatesConnections()
    {
        var loader = new NetworkLoader();
        var network = loader.Load(ValidJson);
        var neighbors = network.GetNeighbors("home");
        var target = Assert.Single(neighbors);

        Assert.Equal("target", target.Hostname);
    }

    [Fact]
    public void Load_FromJson_CreatesBidirectionalConnections()
    {
        var loader = new NetworkLoader();
        var network = loader.Load(ValidJson);
        var neighbors = network.GetNeighbors("target");
        var home = Assert.Single(neighbors);

        Assert.Equal("home", home.Hostname);
    }

    [Fact]
    public void Load_FromDefinition_CreatesNetwork()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();
        var network = loader.Load(definition);

        Assert.Equal(2, network.Servers.Count);

        var home = network.GetServer("home");
        var target = network.GetServer("target");

        Assert.Equal("home", home.Hostname);
        Assert.Equal("target", target.Hostname);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_WithEmptyJson_Throws(string json)
    {
        var loader = new NetworkLoader();

        Assert.Throws<ArgumentException>(() => loader.Load(json));
    }

    [Fact]
    public void Load_WithMalformedJson_Throws()
    {
        var loader = new NetworkLoader();

        const string json = """
                            {
                              "servers":
                            """;

        Assert.Throws<JsonException>(() => loader.Load(json));
    }

    [Fact]
    public void Load_WithMissingServers_Throws()
    {
        var loader = new NetworkLoader();

        const string json = """
                            {
                              "connections": []
                            }
                            """;

        Assert.Throws<JsonException>(() => loader.Load(json));
    }

    [Fact]
    public void Load_WithMissingConnections_Throws()
    {
        var loader = new NetworkLoader();

        const string json = """
                            {
                              "servers": []
                            }
                            """;

        Assert.Throws<JsonException>(() => loader.Load(json));
    }

    [Fact]
    public void Load_WithDuplicateHostname_Throws()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();

        definition = definition with
        {
            Servers =
            [
                CreateServerDefinition(hostname: "n00dles"),
                CreateServerDefinition(hostname: "n00dles")
            ]
        };

        Assert.Throws<InvalidOperationException>(() => loader.Load(definition));
    }

    [Fact]
    public void Load_WithInvalidServerState_Throws()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();

        definition = definition with
        {
            Servers =
            [
                CreateServerDefinition(
                    hostname: "invalid",
                    maximumMoney: 100,
                    startingMoney: 200)
            ]
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => loader.Load(definition));
    }

    [Fact]
    public void Load_WithUnknownConnectionSource_Throws()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();

        definition = definition with
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    From = "unknown",
                    To = "target"
                }
            ]
        };

        Assert.Throws<KeyNotFoundException>(() => loader.Load(definition));
    }

    [Fact]
    public void Load_WithUnknownConnectionDestination_Throws()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();

        definition = definition with
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    From = "home",
                    To = "unknown"
                }
            ]
        };

        Assert.Throws<KeyNotFoundException>(() => loader.Load(definition));
    }

    [Fact]
    public void Load_WithSelfConnection_Throws()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();

        definition = definition with
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    From = "home",
                    To = "home"
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => loader.Load(definition));
    }

    [Fact]
    public void Load_WithMissingConnectionSource_Throws()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();

        definition = definition with
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    From = "",
                    To = "target"
                }
            ]
        };

        Assert.Throws<InvalidDataException>(() => loader.Load(definition));
    }

    [Fact]
    public void Load_WithMissingConnectionDestination_Throws()
    {
        var loader = new NetworkLoader();
        var definition = CreateValidDefinition();

        definition = definition with
        {
            Connections =
            [
                new ConnectionDefinition
                {
                    From = "home",
                    To = ""
                }
            ]
        };

        Assert.Throws<InvalidDataException>(() => loader.Load(definition));
    }

    private static NetworkDefinition CreateValidDefinition()
    {
        return new NetworkDefinition
        {
            Servers =
            [
                CreateServerDefinition(
                    hostname: "home",
                    maximumMoney: 0,
                    startingMoney: 0,
                    hasRootAccess: true),
                CreateServerDefinition(
                    hostname: "target")
            ],
            Connections =
            [
                new ConnectionDefinition
                {
                    From = "home",
                    To = "target"
                }
            ]
        };
    }

    private static ServerDefinition CreateServerDefinition(
        string hostname,
        double maximumMoney = 100_000,
        double startingMoney = 50_000,
        double minimumSecurity = 1,
        double startingSecurity = 2,
        int requiredHackingLevel = 10,
        double growthParameter = 20,
        double maximumRam = 4,
        bool hasRootAccess = false)
    {
        return new ServerDefinition
        {
            Hostname = hostname,
            MaximumMoney = maximumMoney,
            StartingMoney = startingMoney,
            MinimumSecurity = minimumSecurity,
            StartingSecurity = startingSecurity,
            RequiredHackingLevel = requiredHackingLevel,
            GrowthParameter = growthParameter,
            MaximumRam = maximumRam,
            HasRootAccess = hasRootAccess
        };
    }

    private const string ValidJson = """
        {
          "servers": [
            {
              "hostname": "home",
              "maximumMoney": 0,
              "startingMoney": 0,
              "minimumSecurity": 1,
              "startingSecurity": 1,
              "requiredHackingLevel": 1,
              "growthParameter": 0,
              "maximumRam": 8,
              "hasRootAccess": true
            },
            {
              "hostname": "target",
              "maximumMoney": 100000,
              "startingMoney": 50000,
              "minimumSecurity": 1,
              "startingSecurity": 2,
              "requiredHackingLevel": 10,
              "growthParameter": 20,
              "maximumRam": 4,
              "hasRootAccess": false
            }
          ],
          "connections": [
            {
              "from": "home",
              "to": "target"
            }
          ]
        }
        """;
}