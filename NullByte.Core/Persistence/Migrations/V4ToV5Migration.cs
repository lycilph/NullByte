using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates version 4 saves to version 5: servers can have a backdoor installed. No version 4 server
/// has one, so every server starts without.
/// </summary>
public sealed class V4ToV5Migration : ISaveMigration
{
    public int FromVersion => 4;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (save["servers"] is not JsonArray servers)
            return;

        foreach (var node in servers)
        {
            if (node is JsonObject server && server["hasBackdoor"] is null)
                server["hasBackdoor"] = false;
        }
    }
}
