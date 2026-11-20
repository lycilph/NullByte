using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates version 1 saves to version 2 by adding file timestamps.
/// Version 1 did not store per-file timestamps, so the save timestamp is
/// used for both creation and modification time.
/// </summary>
public sealed class V1ToV2Migration : ISaveMigration
{
    public int FromVersion => 1;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        DateTimeOffset savedAtUtc;

        try
        {
            savedAtUtc = save["savedAtUtc"]?.GetValue<DateTimeOffset>() ?? throw new SaveFormatException("The save has no savedAtUtc value.");
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            throw new SaveFormatException("The save has no valid savedAtUtc value.", exception);
        }

        if (save["files"] is not JsonArray files)
            throw new SaveFormatException("The save has no valid files array.");

        foreach (var node in files)
        {
            if (node is not JsonObject file)
                throw new SaveFormatException("The save contains an invalid file entry.");

            file["created"] = savedAtUtc;
            file["modified"] = savedAtUtc;
        }
    }
}
