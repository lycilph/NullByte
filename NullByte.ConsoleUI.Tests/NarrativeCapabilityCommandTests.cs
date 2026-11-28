using NullByte.Core.Domain;
using NullByte.Core.World;
using NullByte.ConsoleUI.Tests.Infrastructure;
using System.Text.RegularExpressions;

namespace NullByte.ConsoleUI.Tests;

public sealed class NarrativeCapabilityCommandTests
{
    [Fact]
    public async Task TutorialStartsWithOnlyStatusAvailable()
    {
        var harness = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Tutorial
        });

        Assert.Equal(["status"], harness.Registry.Commands.Select(command => command.Name));
        Assert.False(harness.Registry.TryGet("help", out _));
        Assert.False(harness.Registry.TryGet("quit", out _));
        Assert.False(harness.Registry.TryGet("cls", out _));
        Assert.False(harness.Registry.TryGet("botnet buy", out _));
        Assert.False(harness.Registry.TryGet("cloud buy", out _));
        Assert.False(harness.Registry.TryGet("debug", out _));

        var output = await harness.RunAndCaptureAsync("help");

        Assert.Contains("Unknown command: help", output);
        Assert.DoesNotContain("Usage:", output);
        Assert.DoesNotContain("Type 'help'", output);
    }

    [Fact]
    public async Task Journal_ShowsGuidanceForUnlockedCapabilities()
    {
        var harness = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Tutorial
        });

        await harness.RunAsync("status");

        var output = await harness.RunAndCaptureAsync("journal");

        var normalizedOutput = output
            .SelectMany(s => s.Split('\n'))
            .Select(s => Regex.Replace(s, @"\s+", " ").Trim())
            .ToList();

        Assert.Contains("SYSTEM JOURNAL", normalizedOutput);
        Assert.Contains("STATUS", normalizedOutput);
        Assert.Contains("NETWORK DISCOVERY", normalizedOutput);
        Assert.DoesNotContain("SERVER OPERATIONS", normalizedOutput);
    }

    [Fact]
    public async Task SuccessfulStatusUnlocksHostControlsAndNetworkScan()
    {
        var harness = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Tutorial
        });

        await harness.RunAsync("status");

        Assert.True(harness.Registry.TryGet("help", out _));
        Assert.True(harness.Registry.TryGet("quit", out _));
        Assert.True(harness.Registry.TryGet("cls", out _));
        Assert.True(harness.Registry.TryGet("scan", out _));
        Assert.False(harness.Registry.TryGet("analyze", out _));
        Assert.False(harness.Registry.TryGet("money", out _));
        Assert.False(harness.Registry.TryGet("skills", out _));
    }

    [Fact]
    public async Task FirstScanUnlocksAnalysisButNotConnection()
    {
        var harness = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            State = InitialGameStateFactory.Create(),
            ProgressionMode = NarrativeProgressionMode.Tutorial
        });

        await harness.RunAsync("status");
        await harness.RunAsync("scan");

        Assert.True(harness.Registry.TryGet("analyze", out _));
        Assert.False(harness.Registry.TryGet("connect", out _));
    }

    [Fact]
    public void FreeplayKeepsAllCommandsAvailableWithoutTutorialState()
    {
        var harness = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Freeplay
        });

        Assert.True(harness.Registry.TryGet("help", out _));
        Assert.True(harness.Registry.TryGet("debug", out _));
        Assert.True(harness.Registry.TryGet("cloud buy", out _));
        Assert.Empty(harness.State.Narrative.CompletedMilestones);
    }
}
