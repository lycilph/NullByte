using NullByte.ConsoleUI.Configuration;

namespace NullByte.ConsoleUI.Tests.Configuration;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_WithNoArguments_ReturnsDefaults()
    {
        var result = CommandLineParser.Parse([]);

        Assert.False(result.StartNewGame);
        Assert.False(result.WriteDefaultSettings);
        Assert.False(result.EnableEditor);
        Assert.Null(result.EditorPort);
        Assert.Null(result.EditorConnectionFilePath);
        Assert.Null(result.SaveDirectory);
        Assert.Null(result.GameSpeedMultiplier);
    }

    [Fact]
    public void Parse_WithNew_SetsStartNewGame()
    {
        var result = CommandLineParser.Parse(["--new"]);

        Assert.True(result.StartNewGame);
    }

    [Fact]
    public void Parse_WithVsCode_EnablesEditor()
    {
        var result = CommandLineParser.Parse(["--vscode"]);

        Assert.True(result.EnableEditor);
    }

    [Fact]
    public void Parse_WithVsCodePort_SetsEditorPort()
    {
        var result = CommandLineParser.Parse([
            "--vscode-port",
            "8765"
        ]);

        Assert.Equal(8765, result.EditorPort);
    }

    [Fact]
    public void Parse_WithVsCodeFile_SetsConnectionFilePath()
    {
        var result = CommandLineParser.Parse([
            "--vscode-file",
            "connection.json"
        ]);

        Assert.Equal(
            "connection.json",
            result.EditorConnectionFilePath);
    }

    [Fact]
    public void Parse_WithSaveDirectory_SetsSaveDirectory()
    {
        var result = CommandLineParser.Parse([
            "--save-directory",
            @"C:\NullByte\Saves"
        ]);

        Assert.Equal(
            @"C:\NullByte\Saves",
            result.SaveDirectory);
    }

    [Fact]
    public void Parse_WithGameSpeed_SetsGameSpeedMultiplier()
    {
        var result = CommandLineParser.Parse([
            "--game-speed",
            "2.5"
        ]);

        Assert.Equal(2.5, result.GameSpeedMultiplier);
    }

    [Fact]
    public void Parse_WithAllOptions_ParsesAllValues()
    {
        var result = CommandLineParser.Parse([
            "--new",
            "--vscode",
            "--vscode-port",
            "8765",
            "--vscode-file",
            "connection.json",
            "--save-directory",
            "TestSaves",
            "--game-speed",
            "5"
        ]);

        Assert.True(result.StartNewGame);
        Assert.True(result.EnableEditor);
        Assert.Equal(8765, result.EditorPort);
        Assert.Equal(
            "connection.json",
            result.EditorConnectionFilePath);
        Assert.Equal("TestSaves", result.SaveDirectory);
        Assert.Equal(5.0, result.GameSpeedMultiplier);
    }

    [Fact]
    public void Parse_WithUnknownArgument_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineParser.Parse(["--unknown"]));

        Assert.Contains(
            "Unknown command-line argument '--unknown'",
            exception.Message);
    }

    [Theory]
    [InlineData("--vscode-port")]
    [InlineData("--vscode-file")]
    [InlineData("--save-directory")]
    [InlineData("--game-speed")]
    public void Parse_WithMissingValue_Throws(string argument)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineParser.Parse([argument]));

        Assert.Contains(
            $"'{argument}' requires a value",
            exception.Message);
    }

    [Fact]
    public void Parse_WithAnotherOptionAsValue_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineParser.Parse([
                "--game-speed",
                "--new"
            ]));

        Assert.Contains(
            "'--game-speed' requires a value",
            exception.Message);
    }

    [Fact]
    public void Parse_WithInvalidInteger_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CommandLineParser.Parse([
                "--vscode-port",
                "abc"
            ]));
    }

    [Fact]
    public void Parse_WithInvalidDouble_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CommandLineParser.Parse([
                "--game-speed",
                "fast"
            ]));
    }

    [Fact]
    public void Parse_WithRepeatedValue_LastValueWins()
    {
        var result = CommandLineParser.Parse([
            "--game-speed",
            "2",
            "--game-speed",
            "5"
        ]);

        Assert.Equal(5.0, result.GameSpeedMultiplier);
    }

    [Fact]
    public void Parse_WithWriteDefaultSettings_SetsFlag()
    {
        var result = CommandLineParser.Parse([
            "--write-default-settings"
        ]);

        Assert.True(result.WriteDefaultSettings);
    }

    [Fact]
    public void Parse_WithWriteDefaultSettingsAndOtherArgument_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CommandLineParser.Parse([
                "--write-default-settings",
                "--new"
            ]));

        Assert.Contains(
            "'--write-default-settings' cannot be combined",
            exception.Message);
    }
}