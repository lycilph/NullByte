using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates version 5 saves to version 6: servers gain immutable IDs and all persisted
/// server relationships are changed from hostnames to those IDs.
/// IDs for legacy saves are deterministic so migration is repeatable.
/// </summary>
public sealed class V5ToV6Migration : ISaveMigration
{
    public int FromVersion => 5;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (save["servers"] is not JsonArray servers)
            throw new InvalidDataException("The save has no server list.");

        var idsByHostname = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in servers)
        {
            if (node is not JsonObject server)
                continue;

            var hostname = server["hostname"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(hostname))
                throw new InvalidDataException("A server has no hostname.");

            if (!idsByHostname.TryAdd(hostname, CreateLegacyId(hostname)))
                throw new InvalidDataException($"The server hostname '{hostname}' occurs more than once.");

            server["id"] = idsByHostname[hostname];
        }

        static string ResolveId(Dictionary<string, string> ids, string hostname, string what)
        {
            if (!ids.TryGetValue(hostname, out var id))
                throw new InvalidDataException($"{what} references unknown server '{hostname}'.");

            return id;
        }

        if (save["player"] is JsonObject player)
        {
            var home = player["homeHostname"]?.GetValue<string>()
                ?? throw new InvalidDataException("The player has no home hostname.");
            var current = player["currentHostname"]?.GetValue<string>()
                ?? throw new InvalidDataException("The player has no current hostname.");

            player["homeServerId"] = ResolveId(idsByHostname, home, "The player's home");
            player["currentServerId"] = ResolveId(idsByHostname, current, "The player's current server");

            if (player["discoveredHostnames"] is JsonArray discovered)
            {
                var ids = new JsonArray();
                foreach (var node in discovered)
                {
                    var hostname = node?.GetValue<string>()
                        ?? throw new InvalidDataException("A discovered server has no hostname.");
                    ids.Add(ResolveId(idsByHostname, hostname, "A discovered server"));
                }

                player["discoveredServerIds"] = ids;
            }
            else
            {
                player["discoveredServerIds"] = new JsonArray();
            }

            player.Remove("homeHostname");
            player.Remove("currentHostname");
            player.Remove("discoveredHostnames");
        }

        if (save["connections"] is JsonArray connections)
        {
            foreach (var node in connections)
            {
                if (node is not JsonObject connection)
                    continue;

                var from = connection["from"]?.GetValue<string>()
                    ?? throw new InvalidDataException("A connection has no source hostname.");
                var to = connection["to"]?.GetValue<string>()
                    ?? throw new InvalidDataException("A connection has no target hostname.");

                connection["fromServerId"] = ResolveId(idsByHostname, from, "A connection");
                connection["toServerId"] = ResolveId(idsByHostname, to, "A connection");
                connection.Remove("from");
                connection.Remove("to");
            }
        }

        if (save["directories"] is JsonArray directories)
        {
            foreach (var node in directories)
            {
                if (node is not JsonObject directory)
                    continue;

                var hostname = directory["hostname"]?.GetValue<string>()
                    ?? throw new InvalidDataException("A directory has no hostname.");
                directory["serverId"] = ResolveId(idsByHostname, hostname, "A directory");
                directory.Remove("hostname");
            }
        }

        if (save["files"] is JsonArray files)
        {
            foreach (var node in files)
            {
                if (node is not JsonObject file)
                    continue;

                var hostname = file["hostname"]?.GetValue<string>()
                    ?? throw new InvalidDataException("A file has no hostname.");
                file["serverId"] = ResolveId(idsByHostname, hostname, "A file");
                file.Remove("hostname");
            }
        }

        if (save["processes"] is JsonArray processes)
        {
            foreach (var node in processes)
            {
                if (node is not JsonObject process)
                    continue;

                var hostname = process["hostname"]?.GetValue<string>()
                    ?? throw new InvalidDataException("A process has no hostname.");
                process["serverId"] = ResolveId(idsByHostname, hostname, "A process");
                process.Remove("hostname");
            }
        }
    }

    private static string CreateLegacyId(string hostname)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("NullByte.Server:" + hostname));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]).ToString("D");
    }
}
