Yes. Mapping this onto the actual codebase, I would keep the refactor entirely inside NullByte.ConsoleUI. The existing split between CompletionCatalog and CompletionEngine is worth preserving because Refresh() runs on the game thread while completion itself runs on the terminal UI thread. repomix-output-lycilph-NullByte.xml

I would make five production-file changes/additions and update the two existing completion test files.

Target structure
NullByte.ConsoleUI/
├── Commands/
│   └── CommandDefinition.cs              MODIFY
├── Terminal/
│   ├── ArgumentCompletion.cs             NEW
│   ├── CompletionKind.cs                 REPLACE ArgumentCompletionKind.cs
│   ├── CompletionCatalog.cs              MODIFY
│   ├── CompletionEngine.cs               MODIFY
│   ├── CompletionSnapshot.cs             MODIFY
│   └── ...
└── CommandRegistryFactory.cs             MODIFY

NullByte.ConsoleUI.Tests/
├── CompletionCatalogTests.cs             MODIFY
└── CompletionEngineTests.cs              MODIFY


CommandLineParser, CompletionResult, ICompletionProvider, and ThreadSafeConsoleTerminal need no changes.





7. Rewrite the argument part of CompletionEngine

The current CompletionEngine explicitly knows about:

ArgumentCompletionKind.BotnetSubcommand
snapshot.BotnetSubcommands
snapshot.BotnetNodes
snapshot.BotnetUpgradeKinds
"upgrade"


That all goes away.

I would replace the entire class with:

namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Pure completion logic: given a snapshot, the input and a cursor
/// position, work out what Tab should do.
///
/// No I/O, game-state access, or knowledge of individual commands.
/// </summary>
public static class CompletionEngine
{
    public static CompletionResult Complete(
        CompletionSnapshot snapshot,
        string input,
        int cursorIndex)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(input);

        cursorIndex =
            Math.Clamp(
                cursorIndex,
                0,
                input.Length);

        var beforeCursor =
            input[..cursorIndex];

        /*
         * Quoted and escaped completion is deliberately not
         * supported yet.
         */
        if (beforeCursor.Contains('"') ||
            beforeCursor.Contains('\\'))
        {
            return CompletionResult.None;
        }

        var tokenStart =
            FindTokenStart(beforeCursor);

        var token =
            beforeCursor[tokenStart..];

        var previousTokens =
            CommandLineParser.Parse(
                beforeCursor[..tokenStart]);

        IReadOnlyList<string> pool;

        if (previousTokens.Count == 0)
        {
            pool =
                snapshot.Commands
                    .Where(command => !command.Hidden)
                    .Select(command => command.Name)
                    .ToArray();
        }
        else
        {
            var command =
                FindCommand(
                    snapshot,
                    previousTokens[0]);

            if (command is null)
                return CompletionResult.None;

            var argumentIndex =
                previousTokens.Count - 1;

            var completion =
                FindCompletion(
                    command,
                    argumentIndex,
                    previousTokens);

            if (completion is null)
                return CompletionResult.None;

            pool =
                GetCompletionValues(
                    snapshot,
                    completion);
        }

        var candidates =
            pool
                .Where(name =>
                    name.StartsWith(
                        token,
                        StringComparison.OrdinalIgnoreCase))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    name => name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (candidates.Length == 0)
            return CompletionResult.None;

        string replacement;

        if (candidates.Length == 1)
        {
            replacement =
                candidates[0];

            var spaceAlreadyFollows =
                cursorIndex < input.Length &&
                char.IsWhiteSpace(
                    input[cursorIndex]);

            if (!spaceAlreadyFollows)
                replacement += " ";
        }
        else
        {
            replacement =
                LongestCommonPrefix(candidates);
        }

        return new CompletionResult(
            tokenStart,
            token.Length,
            replacement,
            candidates);
    }

    private static CommandCompletionInfo? FindCommand(
        CompletionSnapshot snapshot,
        string commandName)
    {
        return snapshot.Commands.FirstOrDefault(
            command =>
                command.Name.Equals(
                    commandName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static ArgumentCompletion? FindCompletion(
        CommandCompletionInfo command,
        int argumentIndex,
        IReadOnlyList<string> tokens)
    {
        return command.Completions.FirstOrDefault(
            completion =>
                completion.ArgumentIndex ==
                    argumentIndex &&
                ConditionMatches(
                    completion.When,
                    tokens));
    }

    private static bool ConditionMatches(
        CompletionCondition? condition,
        IReadOnlyList<string> tokens)
    {
        if (condition is null)
            return true;

        /*
         * tokens[0] is the command name.
         * CompletionCondition.ArgumentIndex is relative to
         * command arguments, so add one.
         */
        var tokenIndex =
            condition.ArgumentIndex + 1;

        if (tokenIndex >= tokens.Count)
            return false;

        return tokens[tokenIndex].Equals(
            condition.Value,
            StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetCompletionValues(
        CompletionSnapshot snapshot,
        ArgumentCompletion completion)
    {
        if (completion.Values.Count > 0)
            return completion.Values;

        if (completion.Kind == CompletionKind.None)
            return [];

        return snapshot.GetValues(
            completion.Kind);
    }

    private static int FindTokenStart(string text)
    {
        var index = text.Length;

        while (index > 0 &&
               !char.IsWhiteSpace(text[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static string LongestCommonPrefix(
        IReadOnlyList<string> values)
    {
        var first = values[0];
        var length = first.Length;

        for (var i = 1;
             i < values.Count && length > 0;
             i++)
        {
            var common = 0;

            var limit =
                Math.Min(
                    length,
                    values[i].Length);

            while (
                common < limit &&
                char.ToUpperInvariant(
                    first[common]) ==
                char.ToUpperInvariant(
                    values[i][common]))
            {
                common++;
            }

            length = common;
        }

        return first[..length];
    }
}


This is the key result of the refactor.

The engine now knows absolutely nothing about:

servers
scripts
botnet
upgrade
level
RAM
cores

It only understands arguments, conditions and candidate strings.

8. CompletionCatalogTests.cs

The existing fixture currently returns only NetworkService and CompletionCatalog.

Change:

private sealed record Fixture(
    NetworkService Network,
    CompletionCatalog Catalog);


to:

private sealed record Fixture(
    GameState GameState,
    NetworkService Network,
    CompletionCatalog Catalog);


And:

private static Fixture CreateFixture()
{
    var gameState =
        InitialGameStateFactory.Create();

    var registry =
        CommandRegistryFactory.Create();

    return new Fixture(
        gameState,
        new NetworkService(gameState),
        new CompletionCatalog(
            registry,
            gameState));
}


Then add script tests.

Run completes scripts
[Fact]
public void Run_OffersScriptsOnCurrentServer()
{
    var fixture = CreateFixture();

    fixture.GameState.FileSystem.WriteFile(
        fixture.GameState.Player.CurrentHostname,
        "hack.cs",
        "nb.Print(\"Hack\");");

    fixture.Catalog.Refresh();

    var result =
        fixture.Catalog.Complete(
            "run h",
            "run h".Length);

    Assert.Equal(
        ["hack.cs"],
        result.Candidates);

    Assert.Equal(
        "hack.cs ",
        result.Replacement);
}

Non-scripts aren't suggested
[Fact]
public void Run_DoesNotOfferNonScriptFiles()
{
    var fixture = CreateFixture();

    var hostname =
        fixture.GameState.Player.CurrentHostname;

    fixture.GameState.FileSystem.WriteFile(
        hostname,
        "hack.cs",
        "");

    fixture.GameState.FileSystem.WriteFile(
        hostname,
        "history.txt",
        "");

    fixture.Catalog.Refresh();

    var result =
        fixture.Catalog.Complete(
            "run h",
            "run h".Length);

    Assert.Contains(
        "hack.cs",
        result.Candidates);

    Assert.DoesNotContain(
        "history.txt",
        result.Candidates);
}

Snapshot behavior also applies to scripts

This is worth preserving because it verifies the threading boundary:

[Fact]
public void Run_NewScriptIsNotVisibleUntilRefresh()
{
    var fixture = CreateFixture();

    fixture.Catalog.Refresh();

    fixture.GameState.FileSystem.WriteFile(
        fixture.GameState.Player.CurrentHostname,
        "hack.cs",
        "");

    Assert.False(
        fixture.Catalog
            .Complete("run h", 5)
            .HasCandidates);

    fixture.Catalog.Refresh();

    Assert.True(
        fixture.Catalog
            .Complete("run h", 5)
            .HasCandidates);
}

9. Rewrite the snapshot in CompletionEngineTests.cs

Instead of this:

new("connect", ArgumentCompletionKind.ConnectableServer)


you now explicitly provide grammar.

The test snapshot becomes:

private static readonly CompletionSnapshot Snapshot =
    new(
        Commands:
        [
            new(
                "help",
                []),

            new(
                "hack",
                []),

            new(
                "home",
                []),

            new(
                "connect",
                [
                    new ArgumentCompletion
                    {
                        ArgumentIndex = 0,
                        Kind =
                            CompletionKind.ConnectableServer
                    }
                ]),

            new(
                "analyze",
                [
                    new ArgumentCompletion
                    {
                        ArgumentIndex = 0,
                        Kind =
                            CompletionKind.KnownServer
                    }
                ]),

            new(
                "run",
                [
                    new ArgumentCompletion
                    {
                        ArgumentIndex = 0,
                        Kind =
                            CompletionKind.ScriptFile
                    }
                ]),

            new(
                "botnet",
                [
                    new ArgumentCompletion
                    {
                        ArgumentIndex = 0,
                        Values =
                        [
                            "status",
                            "buy",
                            "upgrade"
                        ]
                    },

                    new ArgumentCompletion
                    {
                        ArgumentIndex = 1,
                        Kind =
                            CompletionKind.BotnetNode,
                        When =
                            new CompletionCondition(
                                ArgumentIndex: 0,
                                Value: "upgrade")
                    },

                    new ArgumentCompletion
                    {
                        ArgumentIndex = 2,
                        Values =
                        [
                            "level",
                            "ram",
                            "core"
                        ],
                        When =
                            new CompletionCondition(
                                ArgumentIndex: 0,
                                Value: "upgrade")
                    }
                ]),

            new(
                "clear",
                []),

            new(
                "cls",
                []),

            new(
                "time",
                [],
                Hidden: true),

            new(
                "debug",
                [])
        ],

        Values:
            new Dictionary<
                CompletionKind,
                IReadOnlyList<string>>
            {
                [CompletionKind.ConnectableServer] =
                [
                    "n00dles",
                    "foodnstuff",
                    "foodcourt"
                ],

                [CompletionKind.KnownServer] =
                [
                    "home",
                    "n00dles",
                    "sigma-cosmetics"
                ],

                [CompletionKind.ScriptFile] =
                [
                    "hack.cs",
                    "grow.cs",
                    "early-hack.cs"
                ],

                [CompletionKind.BotnetNode] =
                [
                    "1",
                    "2",
                    "3"
                ]
            });


Your existing command and server tests can mostly remain unchanged.

10. Add engine tests for run
[Fact]
public void RunArgument_CompletesAgainstScripts()
{
    var result =
        Complete("run h");

    Assert.Equal(
        "hack.cs ",
        result.Replacement);
}

[Fact]
public void RunArgument_WithNothingTypedOffersAllScripts()
{
    var result =
        Complete("run ");

    Assert.Equal(
        [
            "early-hack.cs",
            "grow.cs",
            "hack.cs"
        ],
        result.Candidates);
}

[Fact]
public void Run_DoesNotCompleteScriptArguments()
{
    var result =
        Complete(
            "run hack.cs n00");

    Assert.False(
        result.HasCandidates);
}

11. Add generic conditional-completion tests

These protect the architectural feature rather than one implementation detail.

For botnet:

[Fact]
public void Botnet_FirstArgument_CompletesSubcommands()
{
    var result =
        Complete("botnet u");

    Assert.Equal(
        "upgrade ",
        result.Replacement);
}

[Fact]
public void Botnet_Upgrade_CompletesNodeNumber()
{
    var result =
        Complete("botnet upgrade ");

    Assert.Equal(
        ["1", "2", "3"],
        result.Candidates);
}

[Fact]
public void Botnet_Upgrade_CompletesUpgradeKind()
{
    var result =
        Complete(
            "botnet upgrade 1 r");

    Assert.Equal(
        "ram ",
        result.Replacement);
}

[Fact]
public void Botnet_Status_DoesNotCompleteSecondArgument()
{
    var result =
        Complete(
            "botnet status ");

    Assert.False(
        result.HasCandidates);
}

[Fact]
public void Botnet_Buy_DoesNotCompleteSecondArgument()
{
    var result =
        Complete(
            "botnet buy ");

    Assert.False(
        result.HasCandidates);
}

12. An additional simplification I would make

Once unique completion always appends a space, this test changes:

public void ConnectArgument_CompletesAgainstConnectableServers()
{
    var result =
        Complete("connect n0");

    Assert.Equal(
        "n00dles ",
        result.Replacement);

    Assert.Equal(
        8,
        result.ReplaceStart);

    Assert.Equal(
        2,
        result.ReplaceLength);
}


Currently it expects "n00dles" without a trailing space because only command and Botnet completion set appendSpaceWhenUnique.

I think "n00dles " is the more natural shell behavior:

connect n0<Tab>
connect n00dles |


just like:

run ha<Tab>
run hack.cs |


So removing appendSpaceWhenUnique is both a simplification and a small UX improvement.

Files that do not need modification

I'd explicitly leave these alone:

CommandLineParser.cs

It still handles token parsing. The completion engine can continue opting out whenever quotes/backslashes occur.

CompletionResult.cs

Its abstraction is already appropriate: replacement span + replacement + candidates.

ICompletionProvider.cs

Still exactly the right boundary between ThreadSafeConsoleTerminal and completion.

ThreadSafeConsoleTerminal.cs

Its CompleteInput() method doesn't care where candidate data comes from. That's good.

VirtualFileSystem.cs

ListFiles(hostname) already gives CompletionCatalog precisely what it needs.

Program.cs

No structural changes. Its existing pattern of refreshing the catalog after every command is still valid and means that edit, rm, connect, scan, and botnet buy all naturally update the next completion snapshot.

Final responsibility map
File/class	Responsibility after refactorCommandDefinition	Stores command metadata and argument completion grammar
ArgumentCompletion	Describes completion for one positional argument
CompletionCondition	Describes when that argument completion applies
CompletionKind	Identifies dynamic completion-data sources
CommandRegistryFactory	Defines actual command grammar
CompletionCatalog	Converts current mutable game state into completion strings
CompletionSnapshot	Immutable bridge from game thread to UI thread
CompletionEngine	Generic token/prefix/condition matching only
ThreadSafeConsoleTerminal	Applies the returned completion to terminal input
VirtualFileSystem	Remains the source of files; knows nothing about completion

The particularly important boundary is:

CommandRegistryFactory
    defines WHAT can be completed

CompletionCatalog
    supplies WHICH dynamic values currently exist

CompletionEngine
    decides HOW completion behaves


That removes the two main smells in the current implementation: CompletionEngine knowing the botnet grammar, and CompletionSnapshot needing a new property for every new feature. repomix-output-lycilph-NullByte.xml

And when M6 gives you commands such as kill <pid>, the intended extension becomes small: add CompletionKind.ProcessId, populate it in CompletionCatalog, and declare argument 0 on kill. The engine stays untouched.