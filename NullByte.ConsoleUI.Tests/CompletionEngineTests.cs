using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Tests;

public sealed class CompletionEngineTests
{
    private static readonly CompletionSnapshot Snapshot = new(
        Commands:
        [
            new("help", ArgumentCompletionKind.None),
            new("hack", ArgumentCompletionKind.None),
            new("home", ArgumentCompletionKind.None),
            new("connect", ArgumentCompletionKind.ConnectableServer),
            new("analyze", ArgumentCompletionKind.KnownServer),
            new("clear", ArgumentCompletionKind.None),
            new("cls", ArgumentCompletionKind.None),
            new("time", ArgumentCompletionKind.None, Hidden: true),
            new("debug", ArgumentCompletionKind.None)
        ],
        ConnectableServers: ["n00dles", "foodnstuff", "foodcourt"],
        KnownServers: ["home", "n00dles", "sigma-cosmetics"]);

    [Fact]
    public void EmptyInput_OffersEveryVisibleCommand()
    {
        var result = Complete(string.Empty);

        Assert.Equal(
            ["analyze", "clear", "cls", "connect", "debug", "hack", "help", "home"],
            result.Candidates);
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

        Assert.Equal("n00dles", result.Replacement);
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

        Assert.Equal("sigma-cosmetics", result.Replacement);
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

        Assert.Equal("n00dles", result.Replacement);
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