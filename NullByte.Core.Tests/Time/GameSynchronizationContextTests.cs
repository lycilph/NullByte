using NullByte.Core.Time;

namespace NullByte.Core.Tests.Time;

public sealed class GameSynchronizationContextTests
{
    [Fact]
    public void IsOnGameThread_ReturnsTrue_OnCreatingThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Assert
        Assert.True(context.IsOnGameThread);
    }

    [Fact]
    public async Task IsOnGameThread_ReturnsFalse_OnAnotherThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act
        bool isOnGameThread = await Task.Run(
            () => context.IsOnGameThread);

        // Assert
        Assert.False(isOnGameThread);
    }

    [Fact]
    public void VerifyAccess_DoesNotThrow_OnGameThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act / Assert
        context.VerifyAccess();
    }

    [Fact]
    public async Task VerifyAccess_Throws_OnAnotherThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Task.Run(context.VerifyAccess));
    }

    [Fact]
    public void Post_ExecutesCallback_WhenQueueIsProcessed()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        int receivedValue = 0;

        // Act
        context.Post(
            state => receivedValue = (int)state!,
            42);

        int processed = context.ProcessPendingCallbacks();

        // Assert
        Assert.Equal(42, receivedValue);
        Assert.Equal(1, processed);
    }

    [Fact]
    public void ProcessPendingCallbacks_ExecutesCallbacksInOrder()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        var receivedValues = new List<int>();

        // Act
        context.Post(state => receivedValues.Add((int)state!), 1);
        context.Post(state => receivedValues.Add((int)state!), 2);
        context.Post(state => receivedValues.Add((int)state!), 3);

        int processed = context.ProcessPendingCallbacks();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, receivedValues);
        Assert.Equal(3, processed);
    }

    [Fact]
    public void ProcessPendingCallbacks_ReturnsZero_WhenQueueIsEmpty()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act
        int processed = context.ProcessPendingCallbacks();

        // Assert
        Assert.Equal(0, processed);
    }

    [Fact]
    public void ProcessPendingCallbacks_RespectsMaximumCallbackCount()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        var receivedValues = new List<int>();

        context.Post(state => receivedValues.Add((int)state!), 1);
        context.Post(state => receivedValues.Add((int)state!), 2);
        context.Post(state => receivedValues.Add((int)state!), 3);

        // Act
        int processed = context.ProcessPendingCallbacks(maxCallbacks: 2);

        // Assert
        Assert.Equal(2, processed);
        Assert.Equal([1, 2], receivedValues);

        // Remaining callback is still queued.
        Assert.Equal(1, context.ProcessPendingCallbacks());
        Assert.Equal([1, 2, 3], receivedValues);
    }

    [Fact]
    public async Task Post_CanBeCalledFromAnotherThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        int receivedValue = 0;

        // Act
        Task.Run(() =>
            context.Post(
                state => receivedValue = (int)state!,
                42))
            .GetAwaiter()
            .GetResult();

        int processed = context.ProcessPendingCallbacks();

        // Assert
        Assert.Equal(42, receivedValue);
        Assert.Equal(1, processed);
    }

    [Fact]
    public void ProcessPendingCallbacks_Throws_WhenCalledFromAnotherThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => Task.Run(() => context.ProcessPendingCallbacks()).GetAwaiter().GetResult());
    }

    [Fact]
    public void Send_ExecutesCallbackImmediately_OnGameThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        int receivedValue = 0;

        // Act
        context.Send(
            state => receivedValue = (int)state!,
            42);

        // Assert
        Assert.Equal(42, receivedValue);
    }

    [Fact]
    public void Send_Throws_WhenCalledFromAnotherThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => Task.Run(() => context.Send(_ => { }, null))
                .GetAwaiter().GetResult());
    }

    [Fact]
    public void RunAsync_StartsOperationOnGameThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        int gameThreadId = Environment.CurrentManagedThreadId;
        int operationThreadId = 0;

        // Act
        Task operation = context.RunAsync(() =>
        {
            operationThreadId = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        });

        // Assert
        Assert.True(operation.IsCompletedSuccessfully);
        Assert.Equal(gameThreadId, operationThreadId);
    }

    [Fact]
    public void RunAsync_ResumesContinuationOnGameThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        int gameThreadId = Environment.CurrentManagedThreadId;
        int initialThreadId = 0;
        int continuationThreadId = 0;

        var completionSource = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        Task operation = context.RunAsync(async () =>
        {
            initialThreadId = Environment.CurrentManagedThreadId;

            await completionSource.Task;

            continuationThreadId = Environment.CurrentManagedThreadId;
        });

        // The operation should now be waiting.
        Assert.False(operation.IsCompleted);

        // Complete the awaited task from another thread.
        Task.Run(completionSource.SetResult)
            .GetAwaiter()
            .GetResult();

        // The continuation is now queued for the game thread.
        Assert.False(operation.IsCompleted);

        int processed = context.ProcessPendingCallbacks();

        // Assert
        Assert.Equal(1, processed);
        Assert.True(operation.IsCompletedSuccessfully);
        Assert.Equal(gameThreadId, initialThreadId);
        Assert.Equal(gameThreadId, continuationThreadId);
    }

    [Fact]
    public void RunAsync_RestoresPreviousSynchronizationContext()
    {
        // Arrange
        var context = new GameSynchronizationContext();
        SynchronizationContext? previousContext =
            SynchronizationContext.Current;

        // Act
        Task operation = context.RunAsync(
            () => Task.CompletedTask);

        // Assert
        Assert.True(operation.IsCompletedSuccessfully);
        Assert.Same(previousContext, SynchronizationContext.Current);
    }

    [Fact]
    public void RunAsync_Throws_WhenCalledFromAnotherThread()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => Task.Run(
                    () => context.RunAsync(() => Task.CompletedTask))
                .GetAwaiter()
                .GetResult());
    }

    [Fact]
    public async Task RunAsync_PropagatesSynchronousExceptionsAsync()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.RunAsync(
                () => throw new InvalidOperationException("Test exception.")));
    }

    [Fact]
    public async Task RunAsync_PropagatesAsynchronousExceptions()
    {
        // Arrange
        var context = new GameSynchronizationContext();

        var completionSource = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Task operation = context.RunAsync(async () =>
        {
            await completionSource.Task;

            throw new InvalidOperationException("Test exception.");
        });

        // Act
        Task.Run(completionSource.SetResult)
            .GetAwaiter()
            .GetResult();

        context.ProcessPendingCallbacks();

        // Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => operation);

        Assert.Equal("Test exception.", exception.Message);
    }
}