using System.Reflection;
using NullByte.ConsoleUI.Configuration;
using NullByte.ConsoleUI.Terminal;
using NullByte.ConsoleUI.Tests.Infrastructure;
using NullByte.Core.Persistence;
using NullByte.Core.Services;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Tests;

public class ActivitySubscriptionLifecycleTests
{
    [Fact]
    public async Task Load_detaches_the_old_sessions_activity_handler()
    {
        var store = new InMemorySaveStore();
        await using var terminal = new ThreadSafeConsoleTerminal();
        var saves = new SaveManager(store, new SaveGameSerializer());
        await SeedSaveAsync(terminal, saves);

        await using var host = CreateHost(terminal, saves);
        await host.StartAsync(resumeAutosave: false);
        var oldSession = host.Session!;
        var oldHandler = GetHostActivityEndedHandler(host);

        AssertActivityEndedHandlerIsSubscribed(oldSession, oldHandler);

        await InvokeHostMethodAsync(host, "LoadAsync", "slot");

        Assert.NotSame(oldSession, host.Session);
        AssertActivityEndedHandlerIsNotSubscribed(oldSession, oldHandler);
        AssertActivityEndedHandlerIsSubscribed(host.Session!, GetHostActivityEndedHandler(host));
    }

    [Fact]
    public async Task Reset_detaches_the_old_sessions_activity_handler()
    {
        var store = new InMemorySaveStore();
        await using var terminal = new ThreadSafeConsoleTerminal();
        var saves = new SaveManager(store, new SaveGameSerializer());

        await using var host = CreateHost(terminal, saves);
        await host.StartAsync(resumeAutosave: false);
        var oldSession = host.Session!;
        var oldHandler = GetHostActivityEndedHandler(host);

        AssertActivityEndedHandlerIsSubscribed(oldSession, oldHandler);

        await InvokeHostMethodAsync(host, "ResetAsync");

        Assert.NotSame(oldSession, host.Session);
        AssertActivityEndedHandlerIsNotSubscribed(oldSession, oldHandler);
        AssertActivityEndedHandlerIsSubscribed(host.Session!, GetHostActivityEndedHandler(host));
    }

    [Fact]
    public async Task Reset_with_freeplay_starts_an_unrestricted_new_session()
    {
        var store = new InMemorySaveStore();
        await using var terminal = new ThreadSafeConsoleTerminal();
        var saves = new SaveManager(store, new SaveGameSerializer());

        await using var host = CreateHost(terminal, saves);
        await host.StartAsync(resumeAutosave: false);

        await InvokeHostMethodAsync(host, "ResetAsync", true);

        Assert.True(host.Session!.State.Narrative.IsFreeplay);
    }

    [Fact]
    public async Task Dispose_detaches_the_current_sessions_activity_handler()
    {
        var store = new InMemorySaveStore();
        await using var terminal = new ThreadSafeConsoleTerminal();
        var saves = new SaveManager(store, new SaveGameSerializer());
        var host = CreateHost(terminal, saves);
        await host.StartAsync(resumeAutosave: false);
        var session = host.Session!;
        var handler = GetHostActivityEndedHandler(host);

        AssertActivityEndedHandlerIsSubscribed(session, handler);

        await host.DisposeAsync();

        AssertActivityEndedHandlerIsNotSubscribed(session, handler);
    }

    [Fact]
    public async Task Failed_load_keeps_the_current_sessions_activity_handler()
    {
        var store = new InMemorySaveStore();
        await using var terminal = new ThreadSafeConsoleTerminal();
        var saves = new SaveManager(store, new SaveGameSerializer());

        await using var host = CreateHost(terminal, saves);
        await host.StartAsync(resumeAutosave: false);
        var session = host.Session!;
        var handler = GetHostActivityEndedHandler(host);

        await InvokeHostMethodAsync(host, "LoadAsync", "missing");

        Assert.Same(session, host.Session);
        AssertActivityEndedHandlerIsSubscribed(session, handler);
    }

    [Fact]
    public async Task Failed_reset_keeps_the_current_sessions_activity_handler()
    {
        var store = new InMemorySaveStore { FailDelete = true };
        await using var terminal = new ThreadSafeConsoleTerminal();
        var saves = new SaveManager(store, new SaveGameSerializer());

        await using var host = CreateHost(terminal, saves);
        await host.StartAsync(resumeAutosave: false);
        var session = host.Session!;
        var handler = GetHostActivityEndedHandler(host);

        await InvokeHostMethodAsync(host, "ResetAsync");

        Assert.Same(session, host.Session);
        AssertActivityEndedHandlerIsSubscribed(session, handler);
    }

    private static GameHost CreateHost(ThreadSafeConsoleTerminal terminal, SaveManager saves)
        => new(terminal, saves, new GameSessionOptions { Output = terminal }, new AppSettings());

    private static async Task SeedSaveAsync(ThreadSafeConsoleTerminal terminal, SaveManager saves)
    {
        await using var session = GameSession.Create(new GameSessionOptions { Output = terminal });
        await session.StartAsync();
        await saves.SaveAsync(session, "slot");
    }

    private static Delegate GetHostActivityEndedHandler(GameHost host)
    {
        var gameProperty = typeof(GameHost).GetProperty(
            "game",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find GameHost.game.");

        var game = gameProperty.GetValue(host)
            ?? throw new InvalidOperationException("GameHost has no active game.");

        var handlerProperty = game.GetType().GetProperty("ActivityEndedHandler")
            ?? throw new InvalidOperationException("Could not find ActivityEndedHandler.");

        return (Delegate)(handlerProperty.GetValue(game)
            ?? throw new InvalidOperationException("ActivityEndedHandler is null."));
    }

    private static Delegate? GetActivityEndedHandlers(GameSession session)
        => typeof(ActivityService)
            .GetField("ActivityEnded", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(session.Services.Activities) as Delegate;

    private static void AssertActivityEndedHandlerIsSubscribed(GameSession session, Delegate handler)
    {
        var handlers = GetActivityEndedHandlers(session)?.GetInvocationList() ?? [];
        Assert.Contains(handler, handlers);
    }

    private static void AssertActivityEndedHandlerIsNotSubscribed(GameSession session, Delegate handler)
    {
        var handlers = GetActivityEndedHandlers(session)?.GetInvocationList() ?? [];
        Assert.DoesNotContain(handler, handlers);
    }

    private static Task InvokeHostMethodAsync(GameHost host, string methodName, params object[] arguments)
    {
        var method = typeof(GameHost).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find GameHost.{methodName}.");

        return (Task)(method.Invoke(host, arguments)
            ?? throw new InvalidOperationException($"GameHost.{methodName} returned no task."));
    }
}
