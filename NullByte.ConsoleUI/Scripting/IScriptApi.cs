namespace NullByte.ConsoleUI.Scripting;

public interface IScriptApi
{
    ServerInfo GetServer(string hostname);

    Task<HackResult> HackAsync(
        string hostname,
        CancellationToken cancellationToken = default);

    void Log(string message);
}