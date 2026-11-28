using NullByte.ConsoleUI.Configuration;

namespace NullByte.ConsoleUI.Tests.Configuration;

public sealed class SettingsLoaderTests
{
    [Fact]
    public void Load_WithNoFileOrArguments_UsesDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        var result = SettingsLoader.Load([], path);

        var expected = SettingsTestData.DefaultSaveDirectory;
        Assert.Equal(expected, result.Settings.SaveDirectory);
        Assert.False(result.Settings.Editor.Enabled);
        Assert.Equal(1.0, result.Settings.Development.GameSpeedMultiplier);
        Assert.True(result.Settings.Startup.ShowBootSequence);
        Assert.False(result.StartNewGame);
    }

    [Fact]
    public void Load_WithSettingsFile_UsesFileValues()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "saveDirectory": "FileSaves",
              "editor": {
                "enabled": true,
                "port": 8000
              },
              "development": {
                "gameSpeedMultiplier": 2.0
              }
            }
            """);

        var result = SettingsLoader.Load([], path);

        Assert.Equal(
            "FileSaves",
            result.Settings.SaveDirectory);
        Assert.True(result.Settings.Editor.Enabled);
        Assert.Equal(8000, result.Settings.Editor.Port);
        Assert.Equal(
            2.0,
            result.Settings.Development.GameSpeedMultiplier);
    }

    [Fact]
    public void Load_WithCommandLine_OverridesFileValues()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "saveDirectory": "FileSaves",
              "editor": {
                "enabled": true,
                "port": 8000
              },
              "development": {
                "gameSpeedMultiplier": 2.0
              }
            }
            """);

        var result = SettingsLoader.Load([
            "--save-directory",
            "CliSaves",
            "--vscode-port",
            "9000",
            "--game-speed",
            "5"
        ], path);

        Assert.Equal(
            "CliSaves",
            result.Settings.SaveDirectory);
        Assert.Equal(9000, result.Settings.Editor.Port);
        Assert.Equal(
            5.0,
            result.Settings.Development.GameSpeedMultiplier);
    }

    [Fact]
    public void Load_WithPartialOverrides_PreservesOtherFileValues()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "saveDirectory": "FileSaves",
              "editor": {
                "enabled": true,
                "port": 8000,
                "connectionFilePath": "editor.json"
              },
              "development": {
                "gameSpeedMultiplier": 2.0
              }
            }
            """);

        var result = SettingsLoader.Load([
            "--game-speed",
            "5"
        ], path);

        Assert.Equal(
            "FileSaves",
            result.Settings.SaveDirectory);

        Assert.True(result.Settings.Editor.Enabled);
        Assert.Equal(8000, result.Settings.Editor.Port);
        Assert.Equal(
            "editor.json",
            result.Settings.Editor.ConnectionFilePath);

        Assert.Equal(
            5.0,
            result.Settings.Development.GameSpeedMultiplier);
    }

    [Fact]
    public void Load_WithNew_SetsLaunchInstruction()
    {
        using var directory = new TemporaryDirectory();

        var result = SettingsLoader.Load(
            ["--new"],
            directory.GetPath("settings.json"));

        Assert.True(result.StartNewGame);
        Assert.False(result.Freeplay);
    }

    [Fact]
    public void Load_WithNewAndFreeplay_SetsBothLaunchInstructions()
    {
        using var directory = new TemporaryDirectory();

        var result = SettingsLoader.Load(
            ["--new", "--freeplay"],
            directory.GetPath("settings.json"));

        Assert.True(result.StartNewGame);
        Assert.True(result.Freeplay);
    }

    [Fact]
    public void Load_WithFreeplayWithoutNew_Throws()
    {
        using var directory = new TemporaryDirectory();

        var exception = Assert.Throws<ArgumentException>(
            () => SettingsLoader.Load(
                ["--freeplay"],
                directory.GetPath("settings.json")));

        Assert.Contains("'--freeplay' requires '--new'", exception.Message);
    }

    [Fact]
    public void Load_WithVsCode_OverridesDisabledFileSetting()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "editor": {
                "enabled": false
              }
            }
            """);

        var result = SettingsLoader.Load(
            ["--vscode"],
            path);

        Assert.True(result.Settings.Editor.Enabled);
    }

    [Fact]
    public void Load_WithInvalidFileSettings_Throws()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            """
            {
              "development": {
                "gameSpeedMultiplier": -2.0
              }
            }
            """);

        Assert.Throws<SettingsValidationException>(
            () => SettingsLoader.Load([], path));
    }

    [Fact]
    public void Load_WithInvalidCommandLineOverride_Throws()
    {
        using var directory = new TemporaryDirectory();

        Assert.Throws<SettingsValidationException>(
            () => SettingsLoader.Load([
                "--game-speed",
                "-5"
            ], directory.GetPath("settings.json")));
    }

    [Fact]
    public void Load_WithWriteDefaultSettings_DoesNotLoadFile()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("settings.json");

        File.WriteAllText(
            path,
            "this is deliberately invalid json");

        var result = SettingsLoader.Load(
            ["--write-default-settings"],
            path);

        Assert.True(result.WriteDefaultSettings);
        Assert.False(result.StartNewGame);

        var expected = SettingsTestData.DefaultSaveDirectory;
        Assert.Equal(expected, result.Settings.SaveDirectory);
    }
}
