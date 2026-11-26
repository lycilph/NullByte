using NullByte.Shell.Persistence;

namespace NullByte.Shell.Tests.Persistence;

public sealed class FileSaveStoreTests : IDisposable
{
    private readonly string directory;

    public FileSaveStoreTests()
    {
        directory = Path.Combine(Path.GetTempPath(), "NullByte.Tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task DeleteAsync_DeletesExistingSave()
    {
        var sut = new FileSaveStore(directory);

        await sut.WriteAsync("autosave", "{}", TestContext.Current.CancellationToken);

        await sut.DeleteAsync("autosave", TestContext.Current.CancellationToken);

        var path = Path.Combine(directory, "autosave.json");

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAsync_DeletesBackup()
    {
        var sut = new FileSaveStore(directory);

        await sut.WriteAsync("autosave", """{"version": 1}""", TestContext.Current.CancellationToken);
        await sut.WriteAsync("autosave", """{"version": 2}""", TestContext.Current.CancellationToken);

        var savePath = Path.Combine(directory, "autosave.json");
        var backupPath = savePath + ".bak";

        Assert.True(File.Exists(savePath));
        Assert.True(File.Exists(backupPath));

        await sut.DeleteAsync("autosave", TestContext.Current.CancellationToken);

        Assert.False(File.Exists(savePath));
        Assert.False(File.Exists(backupPath));
    }

    [Fact]
    public async Task DeleteAsync_WhenSaveDoesNotExist_DoesNotThrow()
    {
        var sut = new FileSaveStore(directory);

        var exception = await Record.ExceptionAsync(
            () => sut.DeleteAsync("autosave", TestContext.Current.CancellationToken));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DeleteAsync_DeletingAutosave_DoesNotDeleteNamedSave()
    {
        var sut = new FileSaveStore(directory);

        await sut.WriteAsync("autosave", "{}", TestContext.Current.CancellationToken);
        await sut.WriteAsync("before_reset", "{}", TestContext.Current.CancellationToken);

        await sut.DeleteAsync("autosave", TestContext.Current.CancellationToken);

        var autosavePath = Path.Combine(
            directory,
            "autosave.json");

        var namedSavePath = Path.Combine(
            directory,
            "before_reset.json");

        Assert.False(File.Exists(autosavePath));
        Assert.True(File.Exists(namedSavePath));
    }

    [Fact]
    public async Task WriteAsync_KeepsThePreviousSaveAsABackup()
    {
        var sut = new FileSaveStore(directory);

        Assert.Empty(await sut.ListAsync(TestContext.Current.CancellationToken));
        Assert.Null(await sut.ReadAsync("one", TestContext.Current.CancellationToken));

        await sut.WriteAsync("one", "first", TestContext.Current.CancellationToken);
        await sut.WriteAsync("one", "second", TestContext.Current.CancellationToken);

        Assert.Equal("second", await sut.ReadAsync("one", TestContext.Current.CancellationToken));
        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(directory, "one.json.bak"), TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "one" }, (await sut.ListAsync(TestContext.Current.CancellationToken)).Select(slot => slot.Name));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("")]
    public async Task WriteAsync_RejectsUnsafeSlotNames(string slot)
    {
        var sut = new FileSaveStore(directory);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.WriteAsync(slot, "x", TestContext.Current.CancellationToken));
    }
}