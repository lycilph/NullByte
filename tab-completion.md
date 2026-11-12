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

1. Replace ArgumentCompletionKind.cs

The current enum mixes dynamic data sources such as KnownServer with the command-specific concept BotnetSubcommand.

Replace it with:

NullByte.ConsoleUI/Terminal/CompletionKind.cs
namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Identifies a dynamic source of tab-completion values.
/// </summary>
public enum CompletionKind
{
    None = 0,

    /// <summary>
    /// Discovered servers the player can connect to right now.
    /// </summary>
    ConnectableServer,

    /// <summary>
    /// Every server the player has discovered.
    /// </summary>
    KnownServer,

    /// <summary>
    /// Script files on the player's current server.
    /// </summary>
    ScriptFile,

    /// <summary>
    /// Existing botnet node numbers.
    /// </summary>
    BotnetNode
}


There is deliberately no:

BotnetSubcommand
BotnetUpgradeKind


Those are static parts of the botnet command grammar, not dynamic game-state concepts.

2. Add ArgumentCompletion.cs
NullByte.ConsoleUI/Terminal/ArgumentCompletion.cs

I would keep this as a simple immutable record:

namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Describes how one positional command argument can be tab-completed.
/// </summary>
public sealed record ArgumentCompletion
{
    /// <summary>
    /// Zero-based argument position after the command name.
    /// </summary>
    public required int ArgumentIndex { get; init; }

    /// <summary>
    /// Dynamic source of completion values.
    /// </summary>
    public CompletionKind Kind { get; init; }

    /// <summary>
    /// Static completion values.
    /// </summary>
    public IReadOnlyList<string> Values { get; init; } = [];

    /// <summary>
    /// Optional condition that must hold for this completion to apply.
    /// </summary>
    public CompletionCondition? When { get; init; }
}

/// <summary>
/// Requires a previous command argument to have a particular value.
/// </summary>
public sealed record CompletionCondition(
    int ArgumentIndex,
    string Value);


I prefer this simple data structure over factory methods because command registration remains very explicit.

For example:

new ArgumentCompletion
{
    ArgumentIndex = 0,
    Kind = CompletionKind.KnownServer
}


versus static values:

new ArgumentCompletion
{
    ArgumentIndex = 0,
    Values = ["status", "buy", "upgrade"]
}

3. Change CommandDefinition.cs

Currently CommandDefinition has this single property:

/// <summary>
/// What the command's first argument can be tab-completed to.
/// </summary>
public ArgumentCompletionKind ArgumentCompletion { get; init; }
    = ArgumentCompletionKind.None;


That is the fundamental limitation. A command can describe only one argument.

Replace it with:

/// <summary>
/// Describes tab completion for the command's positional arguments.
/// </summary>
public IReadOnlyList<ArgumentCompletion> Completions { get; init; }
    = [];


So the complete bottom of CommandDefinition becomes:

public string Name { get; }

public string Usage { get; }

public string Description { get; }

public Func<
    CommandContext,
    IReadOnlyList<string>,
    Task> Handler { get; }

public IReadOnlyList<string> Aliases { get; }

/// <summary>
/// Is this a debugging command.
/// </summary>
public bool Debug { get; init; }

/// <summary>
/// Describes tab completion for the command's positional arguments.
/// </summary>
public IReadOnlyList<ArgumentCompletion> Completions { get; init; }
    = [];


No constructor changes are necessary.

4. Update CommandRegistryFactory.cs

This is where command grammar should live.

Currently analyze, connect, and botnet set ArgumentCompletion, while run has no completion at all.

analyze

Change:

new CommandDefinition(
    "analyze",
    "analyze [hostname]",
    "Show server information.",
    AnalyzeAsync)
{
    ArgumentCompletion =
        ArgumentCompletionKind.KnownServer
}


to:

new CommandDefinition(
    "analyze",
    "analyze [hostname]",
    "Show server information.",
    AnalyzeAsync)
{
    Completions =
    [
        new ArgumentCompletion
        {
            ArgumentIndex = 0,
            Kind = CompletionKind.KnownServer
        }
    ]
}

connect
new CommandDefinition(
    "connect",
    "connect <hostname>",
    "Connect to a discovered server.",
    ConnectAsync)
{
    Completions =
    [
        new ArgumentCompletion
        {
            ArgumentIndex = 0,
            Kind = CompletionKind.ConnectableServer
        }
    ]
}

run

The actual RunScriptAsync resolves the first argument against the current server's virtual filesystem and passes every subsequent argument verbatim into the script.

Therefore only argument zero should be completed:

new CommandDefinition(
    "run",
    "run <script> [args...]",
    "Run a script.",
    RunScriptAsync)
{
    Completions =
    [
        new ArgumentCompletion
        {
            ArgumentIndex = 0,
            Kind = CompletionKind.ScriptFile
        }
    ]
}


Importantly:

run ha<Tab>                yes
run hack.cs n00<Tab>       no


Script arguments remain arbitrary strings.

botnet

This becomes the best example of the generalized model:

new CommandDefinition(
    "botnet",
    "botnet <subcommand>",
    "Manage your botnet (status, buy, upgrade).",
    BotnetAsync)
{
    Completions =
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
            Kind = CompletionKind.BotnetNode,
            When = new CompletionCondition(
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
            When = new CompletionCondition(
                ArgumentIndex: 0,
                Value: "upgrade")
        }
    ]
}


Now CommandRegistryFactory owns the grammar that already exists in BotnetAsync:

botnet status
botnet buy
botnet upgrade <node> level|ram|core


That is much more appropriate than CompletionEngine understanding botnet upgrade.

5. Replace CompletionSnapshot.cs

The current snapshot has one property per domain concept:

ConnectableServers
KnownServers
BotnetSubcommands
BotnetNodes
BotnetUpgradeKinds


That's precisely what makes the implementation grow horizontally whenever another completion source appears.

Replace the file with:

namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Immutable view of everything tab completion may suggest.
/// Built on the game thread, read on the terminal thread.
/// </summary>
public sealed record CompletionSnapshot(
    IReadOnlyList<CommandCompletionInfo> Commands,
    IReadOnlyDictionary<
        CompletionKind,
        IReadOnlyList<string>> Values)
{
    public static CompletionSnapshot Empty { get; } =
        new(
            [],
            new Dictionary<
                CompletionKind,
                IReadOnlyList<string>>());

    public IReadOnlyList<string> GetValues(
        CompletionKind kind)
    {
        return Values.TryGetValue(
            kind,
            out var values)
            ? values
            : [];
    }
}

/// <summary>
/// Completion metadata for a registered command or alias.
/// </summary>
/// <param name="Name">
/// The command name or alias.
/// </param>
/// <param name="Completions">
/// Completion definitions for its arguments.
/// </param>
/// <param name="Hidden">
/// Hidden commands are never suggested, but can still provide
/// argument completion when explicitly entered.
/// </param>
public sealed record CommandCompletionInfo(
    string Name,
    IReadOnlyList<ArgumentCompletion> Completions,
    bool Hidden = false);


This remains immutable from the perspective of the terminal thread.

6. Rewrite CompletionCatalog.Refresh()

This is the file that translates live game state into the immutable completion snapshot.

The existing implementation already correctly does that on the game thread.

Keep that architecture.

Replace Refresh() with:

public void Refresh()
{
    var player = gameState.Player;
    var network = gameState.Network;

    var commands =
        BuildCommands();

    var knownServers =
        player.DiscoveredHostnames
            .Select(hostname =>
                network.TryGetServer(
                    hostname,
                    out var server)
                    ? server
                    : null)
            .OfType<Server>()
            .Select(server => server.Hostname)
            .ToArray();

    var connectableServers =
        network
            .GetNeighbors(player.CurrentHostname)
            .Where(server =>
                player.DiscoveredHostnames.Contains(
                    server.Hostname))
            .Select(server => server.Hostname)
            .ToArray();

    var scriptFiles =
        gameState.FileSystem
            .ListFiles(player.CurrentHostname)
            .Where(IsScriptFile)
            .ToArray();

    var botnetNodes =
        Enumerable
            .Range(
                1,
                gameState.Botnet.NodeCount)
            .Select(node => node.ToString())
            .ToArray();

    var values =
        new Dictionary<
            CompletionKind,
            IReadOnlyList<string>>
        {
            [CompletionKind.ConnectableServer] =
                connectableServers,

            [CompletionKind.KnownServer] =
                knownServers,

            [CompletionKind.ScriptFile] =
                scriptFiles,

            [CompletionKind.BotnetNode] =
                botnetNodes
        };

    snapshot =
        new CompletionSnapshot(
            commands,
            values);
}


Move command construction into:

private IReadOnlyList<CommandCompletionInfo> BuildCommands()
{
    var commands =
        new List<CommandCompletionInfo>();

    foreach (var command in registry.Commands)
    {
        var hidden =
            command.Debug &&
            !command.Name.Equals(
                "debug",
                StringComparison.OrdinalIgnoreCase);

        commands.Add(
            new CommandCompletionInfo(
                command.Name,
                command.Completions,
                hidden));

        foreach (var alias in command.Aliases)
        {
            commands.Add(
                new CommandCompletionInfo(
                    alias,
                    command.Completions,
                    hidden));
        }
    }

    // 'exit' and 'quit' are handled by Program,
    // rather than CommandRegistry.
    foreach (var exit in ExitCommands)
    {
        commands.Add(
            new CommandCompletionInfo(
                exit,
                []));
    }

    return commands;
}


And add:

private static bool IsScriptFile(string fileName)
{
    return fileName.EndsWith(
        ".cs",
        StringComparison.OrdinalIgnoreCase);
}

Why current server?

This is important.

RunScriptAsync does:

var server =
    context.NetworkService.GetCurrentServer();

context.ScriptService.Start(
    server.Hostname,
    fileName,
    scriptArguments);


So completion should use:

player.CurrentHostname


not:

player.HomeHostname


That ensures every script suggested by Tab actually exists in the place run will look.

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