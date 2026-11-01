using NullByte.Core;
using System.Diagnostics;

namespace NullByte.Scripting.Tests;

internal static class Helper
{
    internal static GameSession NewSession() => new(ContentLoader.Load());

    /// <summary>One game tick + one barrier release, with a pause for continuations.</summary>
    internal static async Task TickAsync(GameSession session, ScriptingSystem scripting)
    {
        session.Tick();
        scripting.Tick();
        await Task.Delay(20); // continuations run on the thread pool
    }

    /// <summary>Tick until the condition holds, or maxTicks run out.</summary>
    internal static async Task<bool> RunUntilAsync(GameSession session, ScriptingSystem scripting,
                                                   Func<bool> done, int maxTicks = 300)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < maxTicks && sw.Elapsed.TotalSeconds < 10; i++)
        {
            await TickAsync(session, scripting);
            if (done())
                return true;
        }
        return done();
    }

    internal static long ParseTicks(ScriptInstance script, string prefix)
    {
        var line = script.Output.First(l => l.Contains(prefix));
        string value = line[(line.IndexOf(prefix) + prefix.Length)..];
        int end = 0;
        while (end < value.Length && char.IsDigit(value[end])) end++;
        return long.Parse(value[..end]);
    }
}
