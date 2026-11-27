using NullByte.Core.Domain;

namespace NullByte.Core.Services;

/// <summary>
/// Describes the result of a player connection attempt.
/// </summary>
public sealed record NetworkConnectionResult
{
    private NetworkConnectionResult(bool succeeded, Server? server, string? error)
    {
        Succeeded = succeeded;
        Server = server;
        Error = error;
    }

    public bool Succeeded { get; }

    public Server? Server { get; }

    public string? Error { get; }

    public static NetworkConnectionResult Success(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new NetworkConnectionResult(succeeded: true, server, error: null);
    }

    public static NetworkConnectionResult Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("An error message is required.", nameof(error));

        return new NetworkConnectionResult(succeeded: false, server: null, error);
    }
}