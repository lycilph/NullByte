using NullByte.ConsoleUI.Configuration;

namespace NullByte.ConsoleUI.Tests.Configuration;

public sealed class SettingsFileLoaderTests
{
    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();

        var settings = SettingsFileLoader.Load(directory.GetPath("settings.json"));

        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NullByte", "saves");
        Assert.Equal(expected, settings.SaveDirectory);
        Assert.False(settings.Editor.Enabled);
        Assert.Null(settings.Editor.Port);
        Assert.Equal(1.0, settings.Development.GameSpeedMultiplier);
        Assert.True(settings.Startup.ShowBootSequence);
    }

    [Fact]
    public void Load_WithValidJson_LoadsSettings()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "saveDirectory": "CustomSaves",
              "editor": {
                "enabled": true,
                "port": 9000
              },
              "development": {
                "gameSpeedMultiplier": 5.0
              }
            }
            """);

        var settings = SettingsFileLoader.Load(path);

        Assert.Equal(
            "CustomSaves",
            settings.SaveDirectory);
        Assert.True(settings.Editor.Enabled);
        Assert.Equal(9000, settings.Editor.Port);
        Assert.Equal(
            5.0,
            settings.Development.GameSpeedMultiplier);
    }

    [Fact]
    public void Load_WithComments_AllowsComments()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              // Development speed
              "development": {
                "gameSpeedMultiplier": 5.0
              }
            }
            """);

        var settings = SettingsFileLoader.Load(path);

        Assert.Equal(
            5.0,
            settings.Development.GameSpeedMultiplier);
    }

    [Fact]
    public void Load_WithTrailingComma_AllowsTrailingComma()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "saveDirectory": "Saves",
            }
            """);

        var settings = SettingsFileLoader.Load(path);

        Assert.Equal("Saves", settings.SaveDirectory);
    }

    [Fact]
    public void Load_WithInvalidJson_Throws()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "saveDirectory":
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => SettingsFileLoader.Load(path));

        Assert.Contains(
            "contains invalid JSON",
            exception.Message);
    }

    [Fact]
    public void WriteDefault_WhenFileDoesNotExist_WritesDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        var result = SettingsFileLoader.WriteDefault(path);

        Assert.Equal(path, result);
        Assert.True(File.Exists(path));

        var settings = SettingsFileLoader.Load(path);

        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NullByte", "saves");
        Assert.Equal(expected, settings.SaveDirectory);
        Assert.False(settings.Editor.Enabled);
        Assert.Equal(1.0, settings.Development.GameSpeedMultiplier);
    }

    [Fact]
    public void WriteDefault_WritesCamelCaseJson()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        SettingsFileLoader.WriteDefault(path);

        var json = File.ReadAllText(path);

        Assert.Contains("\"saveDirectory\"", json);
        Assert.Contains("\"editor\"", json);
        Assert.Contains("\"development\"", json);
        Assert.Contains("\"startup\"", json);
        Assert.Contains("\"showBootSequence\": true", json);
        Assert.Contains("\"gameSpeedMultiplier\"", json);

        Assert.DoesNotContain("\"SaveDirectory\"", json);
    }

    [Fact]
    public void WriteDefault_WhenFileExists_DoesNotOverwrite()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        const string existingContent = "do not overwrite";

        File.WriteAllText(path, existingContent);

        Assert.Throws<InvalidOperationException>(
            () => SettingsFileLoader.WriteDefault(path));

        Assert.Equal(
            existingContent,
            File.ReadAllText(path));
    }
}