using System.Text;

namespace NullByte.Shell.Persistence;

public sealed class FileSaveStore(string directory) : ISaveStore
{
    private const string extension = ".json";

    public async Task WriteAsync(string slot, string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        var path = PathFor(slot);
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
            .ConfigureAwait(false);

        if (File.Exists(path))
            File.Replace(temporaryPath, path, path + ".bak");
        else
            File.Move(temporaryPath, path);
    }

    public async Task<string?> ReadAsync(string slot, CancellationToken cancellationToken = default)
    {
        var path = PathFor(slot);

        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public Task DeleteAsync(string slot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = PathFor(slot);

        File.Delete(path);
        File.Delete(path + ".bak");

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
            return Task.FromResult<IReadOnlyList<SaveSlotInfo>>([]);

        IReadOnlyList<SaveSlotInfo> slots = new DirectoryInfo(directory)
            .EnumerateFiles("*" + extension)
            .Where(file => SaveSlot.IsValidName(Path.GetFileNameWithoutExtension(file.Name)))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new SaveSlotInfo(Path.GetFileNameWithoutExtension(file.Name), file.LastWriteTimeUtc))
            .ToList();

        return Task.FromResult(slots);
    }

    private string PathFor(string slot)
    {
        if (!SaveSlot.IsValidName(slot))
            throw new ArgumentException("Save names may only contain letters, digits, '-' and '_'.", nameof(slot));

        return Path.Combine(directory, slot + extension);
    }
}