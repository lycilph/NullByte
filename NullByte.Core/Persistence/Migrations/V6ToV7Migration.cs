using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates version 6 saves to version 7 by adding the cloud-server marker.
/// Existing servers were all created by the game world and are not cloud servers.
/// </summary>
public sealed class V6ToV7Migration : ISaveMigration
{
    public int FromVersion => 6;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (save["servers"] is not JsonArray servers)
            throw new InvalidDataException("The save has no server list.");

        foreach (var node in servers)
        {
            if (node is not JsonObject server)
                continue;

            server["isCloudServer"] = false;
        }
    }
}
