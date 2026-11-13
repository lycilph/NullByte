using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Tests;

public sealed class CompletionEngineTests
{
    private static readonly CompletionSnapshot Snapshot =
    new(
        Commands:
        [
            new("help", []),
            new("hack", []),
            new("home", []),
            new("connect",
                [
                    new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.ConnectableServer }
                ]),
            new("analyze",
                [
                    new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.KnownServer }
                ]),
            new("run",
                [
                    new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Filename }
                ]),
            new("botnet upgrade",
                [
                    new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.BotnetNode },
                    new ArgumentCompletion { ArgumentIndex = 1, Values = ["level", "ram", "core"] }
                ]),
            new("botnet", []),
            new("botnet buy", []),
            new("clear", []),
            new("cls", []),
            new("time", [], Hidden: true),
            new("debug", [])
        ],
        
        Values:
            new Dictionary<CompletionKind, IReadOnlyList<string>>
            {
                [CompletionKind.ConnectableServer] = ["n00dles", "foodnstuff", "foodcourt"],
                [CompletionKind.KnownServer] = ["home", "n00dles", "sigma-cosmetics"],
                [CompletionKind.Filename] = ["hack.cs", "grow.cs", "early-hack.cs"],
                [CompletionKind.BotnetNode] = ["1", "2", "3"]
            });

    [Fact]
    public void EmptyInput_OffersEveryVisibleCommand()
    {
        var result = Complete(string.Empty);

        Assert.Equal(["analyze", "botnet", "clear", "cls", "connect", "debug", "hack", "help", "home", "run"], result.Candidates);
        Assert.Equal(string.Empty, result.Replacement);
    }

    [Fact]
    public void HiddenCommands_AreNeverSuggested()
    {
        var result = Complete("t");

        Assert.False(result.HasCandidates);
    }

    [Fact]
    public void UniqueCommandPrefix_CompletesAndAppendsSpace()
    {
        var result = Complete("he");

        Assert.Equal("help ", result.Replacement);
        Assert.Equal(0, result.ReplaceStart);
        Assert.Equal(2, result.ReplaceLength);
        Assert.Single(result.Candidates);
    }

    [Fact]
    public void CommandCompletion_IsCaseInsensitive()
    {
        var result = Complete("HA");

        Assert.Equal("hack ", result.Replacement);
    }

    [Fact]
    public void AmbiguousCommandPrefix_ReturnsAllCandidatesAndTheCommonPrefix()
    {
        var result = Complete("h");

        Assert.Equal(["hack", "help", "home"], result.Candidates);
        Assert.Equal("h", result.Replacement);
    }

    [Fact]
    public void AmbiguousCommandPrefix_ExtendsToTheLongestCommonPrefix()
    {
        var result = Complete("c");

        // clear, cls, connect share only "c".
        Assert.Equal("c", result.Replacement);

        result = Complete("cl");

        Assert.Equal(["clear", "cls"], result.Candidates);
        Assert.Equal("cl", result.Replacement);
    }

    [Fact]
    public void UnknownCommandPrefix_HasNoCandidates()
    {
        Assert.False(Complete("zzz").HasCandidates);
    }

    [Fact]
    public void ConnectArgument_CompletesAgainstConnectableServers()
    {
        var result = Complete("connect n0");

        Assert.Equal("n00dles ", result.Replacement);
        Assert.Equal(8, result.ReplaceStart);
        Assert.Equal(2, result.ReplaceLength);
    }

    [Fact]
    public void ConnectArgument_ExtendsToCommonPrefix()
    {
        var result = Complete("connect f");

        Assert.Equal(["foodcourt", "foodnstuff"], result.Candidates);
        Assert.Equal("food", result.Replacement);
    }

    [Fact]
    public void ConnectArgument_WithNothingTypedOffersEverything()
    {
        var result = Complete("connect ");

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(8, result.ReplaceStart);
        Assert.Equal(0, result.ReplaceLength);
        Assert.Equal(string.Empty, result.Replacement);
    }

    [Fact]
    public void AnalyzeArgument_CompletesAgainstKnownServers()
    {
        var result = Complete("analyze s");

        Assert.Equal("sigma-cosmetics ", result.Replacement);
    }

    [Fact]
    public void RunArgument_CompletesAgainstScripts()
    {
        var result = Complete("run h");

        Assert.Equal("hack.cs ", result.Replacement);
    }

    [Fact]
    public void RunArgument_WithNothingTypedOffersAllScripts()
    {
        var result = Complete("run ");

        Assert.Equal(["early-hack.cs", "grow.cs", "hack.cs"], result.Candidates);
    }

    [Fact]
    public void Run_DoesNotCompleteScriptArguments()
    {
        var result = Complete("run hack.cs n00");

        Assert.False(result.HasCandidates);
    }

    [Fact]
    public void Botnet_FirstArgument_CompletesSubcommands()
    {
        var result = Complete("botnet u");

        Assert.Equal("upgrade ", result.Replacement);
    }

    [Fact]
    public void Botnet_Upgrade_CompletesNodeNumber()
    {
        var result = Complete("botnet upgrade ");

        Assert.Equal(["1", "2", "3"], result.Candidates);
    }

    [Fact]
    public void Botnet_Upgrade_CompletesUpgradeKind()
    {
        var result = Complete("botnet upgrade 1 r");

        Assert.Equal("ram ", result.Replacement);
    }

    [Fact]
    public void Botnet_Status_DoesNotCompleteSecondArgument()
    {
        var result = Complete("botnet status ");

        Assert.False(result.HasCandidates);
    }

    [Fact]
    public void Botnet_Buy_DoesNotCompleteSecondArgument()
    {
        var result = Complete("botnet buy ");

        Assert.False(result.HasCandidates);
    }

    [Fact]
    public void CommandWithoutArgumentCompletion_OffersNothingForArguments()
    {
        Assert.False(Complete("hack n").HasCandidates);
    }

    [Fact]
    public void OnlyTheFirstArgumentIsCompleted()
    {
        Assert.False(Complete("connect n00dles n").HasCandidates);
    }

    [Fact]
    public void CommandNameInArgumentPosition_IsMatchedCaseInsensitively()
    {
        var result = Complete("CONNECT n0");

        Assert.Equal("n00dles ", result.Replacement);
    }

    [Fact]
    public void QuotedText_IsNotCompleted()
    {
        Assert.False(Complete("connect \"n0").HasCandidates);
    }

    [Fact]
    public void CursorInTheMiddle_CompletesTextBeforeCursorOnly()
    {
        var result = CompletionEngine.Complete(Snapshot, "he connect", cursorIndex: 2);

        // A space already follows the cursor, so none is added.
        Assert.Equal("help", result.Replacement);
        Assert.Equal(0, result.ReplaceStart);
        Assert.Equal(2, result.ReplaceLength);
    }

    [Fact]
    public void CursorBeyondEnd_IsClamped()
    {
        var result = CompletionEngine.Complete(Snapshot, "he", cursorIndex: 99);

        Assert.Equal("help ", result.Replacement);
    }

    [Fact]
    public void EmptySnapshot_HasNoCandidates()
    {
        var result = CompletionEngine.Complete(CompletionSnapshot.Empty, "h", 1);

        Assert.False(result.HasCandidates);
    }

    private static CompletionResult Complete(string input)
        => CompletionEngine.Complete(Snapshot, input, input.Length);
}