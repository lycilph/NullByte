using System.Text.Json.Nodes;
using NullByte.Core.Persistence.Migrations;

namespace NullByte.Core.Persistence;

/// <summary>
/// Upgrades a save by one format version. Migrations work on the raw JSON, because the
/// typed <see cref="SaveGame"/> only describes the newest format.
/// </summary>
public interface ISaveMigration
{
    /// <summary>The version this migration reads. It produces <c>FromVersion + 1</c>.</summary>
    int FromVersion { get; }

    /// <summary>Rewrites the save in place. The migrator updates the version number.</summary>
    void Migrate(JsonObject save);
}

/// <summary>
/// Upgrades old saves to <see cref="SaveGame.CurrentVersion"/> by running migrations in order.
/// </summary>
public sealed class SaveMigrator
{
    private readonly Dictionary<int, ISaveMigration> migrations;
    private readonly int currentVersion;

    /// <param name="migrations">Registered migrations. If omitted, the built-in save migrations are used.</param>
    /// <param name="currentVersion">The version to upgrade to. Only tests override it.</param>
    public SaveMigrator(IEnumerable<ISaveMigration>? migrations = null, int currentVersion = SaveGame.CurrentVersion)
    {
        this.currentVersion = currentVersion;
        this.migrations = (migrations ??
        [
            new V1ToV2Migration(),
            new V2ToV3Migration(),
            new V3ToV4Migration(),
            new V4ToV5Migration(),
            new V5ToV6Migration(),
            new V6ToV7Migration(),
            new V7ToV8Migration(),
            new V8ToV9Migration()
        ])
            .ToDictionary(migration => migration.FromVersion);
    }

    public void Upgrade(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        var version = ReadVersion(save);

        if (version > currentVersion)
        {
            throw new SaveFormatException(
                $"This save was written by a newer version of NullByte (save format {version}; " +
                $"this version reads up to {currentVersion}).");
        }

        while (version < currentVersion)
        {
            if (!migrations.TryGetValue(version, out var migration))
                throw new SaveFormatException($"This save is too old to load (no way to upgrade save format {version}).");

            migration.Migrate(save);

            version++;
            save["version"] = version;
        }
    }

    private static int ReadVersion(JsonObject save)
    {
        try
        {
            var version = save["version"]?.GetValue<int>();

            if (version is >= 1)
                return version.Value;
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            // Falls through to the error below.
        }

        throw new SaveFormatException("The save has no valid version number.");
    }
}
