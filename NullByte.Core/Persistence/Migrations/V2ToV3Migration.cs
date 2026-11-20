using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates version 2 saves to version 3 by adding the list of directories.
/// Version 2 had no directories, so the list starts empty. Files restore the
/// directories above them on their own.
/// </summary>
public sealed class V2ToV3Migration : ISaveMigration
{
    public int FromVersion => 2;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (save["directories"] is null)
            save["directories"] = new JsonArray();
    }
}