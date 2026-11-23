namespace NullByte.ConsoleUI;

/// <summary>
/// Carries a request from a command running inside the current game
/// session to the host that owns and can replace that session.
/// </summary>
public sealed class ResetRequest
{
    private bool requested;

    /// <summary>
    /// Requests that the current game be replaced with a new game.
    /// </summary>
    public void Request()
    {
        requested = true;
    }

    /// <summary>
    /// Returns whether a reset was requested and clears the request.
    /// </summary>
    public bool Take()
    {
        var result = requested;
        requested = false;
        return result;
    }
}