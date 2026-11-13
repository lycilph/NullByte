namespace NullByte.Core.Simulation;

public interface IGameThread
{
    public bool IsGameThread { get; }
    public bool IsRunning { get; }

    public Task InvokeAsync(Func<Task> callback);
    public Task<T> InvokeAsync<T>(Func<Task<T>> callback);
}
