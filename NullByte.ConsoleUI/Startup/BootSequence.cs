namespace NullByte.ConsoleUI.Startup;

/// <summary>
/// Renders the fictional NullByte system boot sequence before the interactive terminal starts.
/// Renders startup progress and waits for required initialization before the terminal starts.
/// </summary>
public sealed class BootSequence
{
    private const int FinalSequenceDelay = 1_000;
    private const int MinimumStepDelayMilliseconds = 500;
    private const int MaximumStepDelayMilliseconds = 1_000;
    private const int ProgressFieldWidth = 30;
    private static readonly TimeSpan ReadyingBlinkInterval = TimeSpan.FromMilliseconds(500);

    private static readonly string[] BootMessages =
    [
        "Memory map verified",
        "Runtime environment linked",
        "Network interface initialized",
        "Security subsystem loaded",
        "Userland services mounted",
        "Command interpreter standing by",
        "Terminal session prepared"
    ];

    private static readonly int MaximumMessageLength = BootMessages.Max(message => message.Length);
    private static readonly int StatusColumn = 2 + MaximumMessageLength + 1 + ProgressFieldWidth;

    private readonly TextWriter output;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Random random;

    public BootSequence(TextWriter output, Func<TimeSpan, CancellationToken, Task>? delay = null, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        this.output = output;
        this.delay = delay ?? Task.Delay;
        this.random = random ?? Random.Shared;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default, Func<CancellationToken, Task>? warmUp = null)
    {
        var warmUpTask = warmUp?.Invoke(cancellationToken);

        await output.WriteLineAsync(@"
        ___           ___           ___       ___       ___           ___           ___           ___     
       /\__\         /\__\         /\__\     /\__\     /\  \         |\__\         /\  \         /\  \    
      /::|  |       /:/  /        /:/  /    /:/  /    /::\  \        |:|  |        \:\  \       /::\  \   
     /:|:|  |      /:/  /        /:/  /    /:/  /    /:/\:\  \       |:|  |         \:\  \     /:/\:\  \  
    /:/|:|  |__   /:/  /  ___   /:/  /    /:/  /    /::\~\:\__\      |:|__|__       /::\  \   /::\~\:\  \ 
   /:/ |:| /\__\ /:/__/  /\__\ /:/__/    /:/__/    /:/\:\ \:|__|     /::::\__\     /:/\:\__\ /:/\:\ \:\__\
   \/__|:|/:/  / \:\  \ /:/  / \:\  \    \:\  \    \:\~\:\/:/  /    /:/~~/~       /:/  \/__/ \:\~\:\ \/__/
       |:/:/  /   \:\  /:/  /   \:\  \    \:\  \    \:\ \::/  /    /:/  /        /:/  /       \:\ \:\__\  
       |::/  /     \:\/:/  /     \:\  \    \:\  \    \:\/:/  /     \/__/         \/__/         \:\ \/__/  
       /:/  /       \::/  /       \:\__\    \:\__\    \::/__/                                   \:\__\    
       \/__/         \/__/         \/__/     \/__/     ~~                                        \/__/    ");
        await output.WriteLineAsync();
        await output.WriteLineAsync("  NULLBYTE SYSTEMS  /  INTERACTIVE ENVIRONMENT");
        await output.WriteLineAsync("  ------------------------------------------------------------------------------------------------");
        await output.WriteLineAsync("  INITIALIZING SYSTEM");
        await output.FlushAsync(cancellationToken);

        foreach (var message in BootMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefix = $"  {message}";
            var dotCount = (StatusColumn - prefix.Length) / 2;
            var totalDelayMilliseconds = random.Next(MinimumStepDelayMilliseconds, MaximumStepDelayMilliseconds + 1);
            var delayPerDot = totalDelayMilliseconds / dotCount;
            var remainingMilliseconds = totalDelayMilliseconds % dotCount;

            await output.WriteAsync(prefix);
            await output.FlushAsync(cancellationToken);

            for (var dot = 0; dot < dotCount; dot++)
            {
                var dotDelay = delayPerDot + (dot < remainingMilliseconds ? 1 : 0);
                await delay(TimeSpan.FromMilliseconds(dotDelay), cancellationToken);
                await output.WriteAsync(" .");
                await output.FlushAsync(cancellationToken);
            }

            var padding = StatusColumn - (prefix.Length + dotCount * 2);
            await output.WriteAsync(new string(' ', padding));
            await output.WriteLineAsync(" [ OK ]");
        }
        await output.WriteLineAsync();

        var showedReadyingStatus = false;
        if (warmUpTask is not null)
        {
            if (warmUpTask.IsCompleted)
                await warmUpTask;
            else
            {
                await WaitWithReadyingStatusAsync(warmUpTask, cancellationToken);
                showedReadyingStatus = true;
            }
        }

        if (showedReadyingStatus)
        {
            await output.WriteAsync("\r  SYSTEM READY     ");
            await output.WriteLineAsync();
        }
        else
        {
            await output.WriteLineAsync();
            await output.WriteLineAsync("  SYSTEM READY");
        }
        await output.WriteLineAsync();
        await output.FlushAsync(cancellationToken);
        await delay(TimeSpan.FromMilliseconds(FinalSequenceDelay), cancellationToken);
    }

    private async Task WaitWithReadyingStatusAsync(Task warmUpTask, CancellationToken cancellationToken)
    {
        var showStatus = true;
        while (!warmUpTask.IsCompleted)
        {
            await output.WriteAsync(showStatus ? "\r  SYSTEM READYING" : "\r                 ");
            await output.FlushAsync(cancellationToken);
            showStatus = !showStatus;

            // Do not wait for the remainder of a blink interval after warm-up completes.
            var blinkDelay = delay(ReadyingBlinkInterval, cancellationToken);
            await Task.WhenAny(warmUpTask, blinkDelay);
        }

        await warmUpTask;
        await output.FlushAsync(cancellationToken);
    }
}
