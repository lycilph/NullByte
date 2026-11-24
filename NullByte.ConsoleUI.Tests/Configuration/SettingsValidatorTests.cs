using NullByte.ConsoleUI.Configuration;

namespace NullByte.ConsoleUI.Tests.Configuration;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void Validate_WithDefaults_DoesNotThrow()
    {
        var settings = new AppSettings();

        var exception = Record.Exception(
            () => SettingsValidator.Validate(settings));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Validate_WithEmptySaveDirectory_Throws(
        string saveDirectory)
    {
        var settings = new AppSettings
        {
            SaveDirectory = saveDirectory
        };

        var exception =
            Assert.Throws<SettingsValidationException>(
                () => SettingsValidator.Validate(settings));

        Assert.Contains(
            "SaveDirectory cannot be empty.",
            exception.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Validate_WithInvalidEditorPort_Throws(int port)
    {
        var settings = new AppSettings
        {
            Editor = new EditorSettings
            {
                Port = port
            }
        };

        var exception =
            Assert.Throws<SettingsValidationException>(
                () => SettingsValidator.Validate(settings));

        Assert.Contains(
            "Editor.Port must be between 1 and 65535.",
            exception.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8765)]
    [InlineData(65535)]
    public void Validate_WithValidEditorPort_DoesNotThrow(int port)
    {
        var settings = new AppSettings
        {
            Editor = new EditorSettings
            {
                Port = port
            }
        };

        var exception = Record.Exception(
            () => SettingsValidator.Validate(settings));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithEmptyConnectionFilePath_Throws(
        string path)
    {
        var settings = new AppSettings
        {
            Editor = new EditorSettings
            {
                ConnectionFilePath = path
            }
        };

        var exception =
            Assert.Throws<SettingsValidationException>(
                () => SettingsValidator.Validate(settings));

        Assert.Contains(
            "Editor.ConnectionFilePath cannot be empty.",
            exception.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithNonPositiveGameSpeed_Throws(
        double multiplier)
    {
        var settings = new AppSettings
        {
            Development = new DevelopmentSettings
            {
                GameSpeedMultiplier = multiplier
            }
        };

        var exception =
            Assert.Throws<SettingsValidationException>(
                () => SettingsValidator.Validate(settings));

        Assert.Contains(
            "Development.GameSpeedMultiplier must be "
            + "a finite number greater than 0.",
            exception.Errors);
    }

    [Fact]
    public void Validate_WithNaNGameSpeed_Throws()
    {
        AssertInvalidGameSpeed(double.NaN);
    }

    [Fact]
    public void Validate_WithPositiveInfinityGameSpeed_Throws()
    {
        AssertInvalidGameSpeed(double.PositiveInfinity);
    }

    [Fact]
    public void Validate_WithNegativeInfinityGameSpeed_Throws()
    {
        AssertInvalidGameSpeed(double.NegativeInfinity);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        var settings = new AppSettings
        {
            SaveDirectory = "",
            Editor = new EditorSettings
            {
                Port = 70000
            },
            Development = new DevelopmentSettings
            {
                GameSpeedMultiplier = 0
            }
        };

        var exception =
            Assert.Throws<SettingsValidationException>(
                () => SettingsValidator.Validate(settings));

        Assert.Equal(3, exception.Errors.Count);
        Assert.Contains(
            "SaveDirectory cannot be empty.",
            exception.Errors);
        Assert.Contains(
            "Editor.Port must be between 1 and 65535.",
            exception.Errors);
        Assert.Contains(
            "Development.GameSpeedMultiplier must be "
            + "a finite number greater than 0.",
            exception.Errors);
    }

    private static void AssertInvalidGameSpeed(double multiplier)
    {
        var settings = new AppSettings
        {
            Development = new DevelopmentSettings
            {
                GameSpeedMultiplier = multiplier
            }
        };

        Assert.Throws<SettingsValidationException>(
            () => SettingsValidator.Validate(settings));
    }

    [Fact]
    public void Validate_WithNullEditor_Throws()
    {
        var settings = new AppSettings
        {
            Editor = null!
        };

        var exception =
            Assert.Throws<SettingsValidationException>(
                () => SettingsValidator.Validate(settings));

        Assert.Contains(
            "Editor settings cannot be null.",
            exception.Errors);
    }

    [Fact]
    public void Validate_WithNullDevelopment_Throws()
    {
        var settings = new AppSettings
        {
            Development = null!
        };

        var exception =
            Assert.Throws<SettingsValidationException>(
                () => SettingsValidator.Validate(settings));

        Assert.Contains(
            "Development settings cannot be null.",
            exception.Errors);
    }
}