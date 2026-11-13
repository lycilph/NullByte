namespace NullByte.ConsoleUI;

public sealed class ShutdownRequest
{
    private readonly CancellationTokenSource source = new();

    public bool IsRequested => source.IsCancellationRequested;
    public CancellationToken Token => source.Token;

    public void Request() => source.Cancel();
}