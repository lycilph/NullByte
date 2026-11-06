using System.Text;
using System.Threading.Channels;

namespace NullByte.ConsoleUI.Terminal;

public sealed class ThreadSafeConsoleTerminal : IGameTerminal, IAsyncDisposable
{
    private const int RefreshDelayMilliseconds = 16;

    private readonly int maximumHistoryLines;
    private string prompt;

    /*
     * Multiple background threads may write to this channel.
     * Only the UI loop reads from it.
     */
    private readonly Channel<TerminalRequest> requests =
        Channel.CreateUnbounded<TerminalRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

    /*
     * Only the UI loop writes commands here.
     * The game command processor reads them.
     */
    private readonly Channel<string> commands =
        Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });

    private readonly CancellationTokenSource stopSource = new();

    // These fields are accessed only by the UI loop.
    private readonly List<string> history = [];
    private readonly StringBuilder input = new();

    private int cursorIndex;
    private int lastWindowWidth = -1;
    private int lastWindowHeight = -1;
    private bool screenInvalidated = true;
    private bool isInputEnabled = true;
    private bool hasStarted;
    private bool hasDisposed;

    public ThreadSafeConsoleTerminal(string prompt = "> ", int maximumHistoryLines = 1_000)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentException(
                "The prompt cannot be empty.",
                nameof(prompt));
        }

        if (maximumHistoryLines < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumHistoryLines),
                "Maximum history lines must be at least one.");
        }

        this.prompt = prompt;
        this.maximumHistoryLines = maximumHistoryLines;
    }

    public CancellationToken StoppingToken => stopSource.Token;

    public void SetInputEnabled(bool enabled)
    {
        if (hasDisposed || stopSource.IsCancellationRequested)
            return;

        requests.Writer.TryWrite(new SetInputEnabledRequest(enabled));
    }

    public void SetPrompt(string prompt)
    {
        if (hasDisposed || stopSource.IsCancellationRequested)
            return;

        requests.Writer.TryWrite(new SetPromptRequest(prompt));
    }

    public void WriteLine() => WriteLine(string.Empty);

    public void WriteLine(string message)
    {
        if (hasDisposed || stopSource.IsCancellationRequested)
            return;

        /*
         * Split embedded newlines into distinct logical history entries.
         * Normalize Windows and old Mac line endings first.
         */
        var lines = (message ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var line in lines)
        {
            requests.Writer.TryWrite(
                new AppendLineRequest(line));
        }
    }

    public void Clear()
    {
        if (hasDisposed || stopSource.IsCancellationRequested)
            return;

        requests.Writer.TryWrite(ClearRequest.Instance);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (hasStarted)
            throw new InvalidOperationException("The terminal UI loop can only be started once.");

        hasStarted = true;

        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopSource.Token);

        var token = linkedSource.Token;

        try
        {
            InitializeConsole();

            while (!token.IsCancellationRequested)
            {
                var changed = ProcessPendingRequests();

                changed |= ProcessKeyboardInput();

                var currentWidth = SafeWindowWidth();
                var currentHeight = SafeWindowHeight();

                if (currentWidth != lastWindowWidth ||
                    currentHeight != lastWindowHeight)
                {
                    lastWindowWidth = currentWidth;
                    lastWindowHeight = currentHeight;
                    changed = true;
                }

                if (changed || screenInvalidated)
                {
                    Render();
                    screenInvalidated = false;
                }

                await Task.Delay(
                    RefreshDelayMilliseconds,
                    token);
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            commands.Writer.TryComplete();
            RestoreConsole();
        }
    }

    public IAsyncEnumerable<string> ReadCommandsAsync(
        CancellationToken cancellationToken = default)
    {
        return commands.Reader.ReadAllAsync(cancellationToken);
    }

    public void RequestStop()
    {
        if (!stopSource.IsCancellationRequested)
            stopSource.Cancel();
    }

    private void InitializeConsole()
    {
        try
        {
            Console.CursorVisible = false;
            Console.Clear();
        }
        catch
        {
            /*
             * Some hosts do not support cursor manipulation.
             * Rendering will make another attempt and provide a clearer
             * exception if the host is fundamentally incompatible.
             */
        }

        screenInvalidated = true;
    }

    private void RestoreConsole()
    {
        try
        {
            var finalRow = Math.Max(0, SafeWindowHeight() - 1);

            Console.SetCursorPosition(0, finalRow);
            Console.WriteLine();
            Console.CursorVisible = true;
        }
        catch
        {
            // The console may already be closing.
        }
    }

    private bool ProcessPendingRequests()
    {
        var changed = false;

        while (requests.Reader.TryRead(out var request))
        {
            switch (request)
            {
                case SetPromptRequest setPrompt:
                    prompt = setPrompt.Prompt;
                    changed = true;
                    break;

                case AppendLineRequest append:
                    history.Add(append.Message);
                    TrimHistory();
                    changed = true;
                    break;

                case ClearRequest:
                    history.Clear();
                    changed = true;
                    break;

                case SetInputEnabledRequest setInputEnabled:
                    isInputEnabled = setInputEnabled.Enabled;

                    if (!isInputEnabled)
                    {
                        input.Clear();
                        cursorIndex = 0;
                    }

                    changed = true;
                    break;
            }
        }

        return changed;
    }

    private bool ProcessKeyboardInput()
    {
        var changed = false;

        while (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            
            if (!isInputEnabled)
                continue;

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    SubmitCurrentInput();
                    changed = true;
                    break;

                case ConsoleKey.Backspace:
                    if (cursorIndex > 0)
                    {
                        input.Remove(cursorIndex - 1, 1);
                        cursorIndex--;
                        changed = true;
                    }

                    break;

                case ConsoleKey.Delete:
                    if (cursorIndex < input.Length)
                    {
                        input.Remove(cursorIndex, 1);
                        changed = true;
                    }

                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorIndex > 0)
                    {
                        cursorIndex--;
                        changed = true;
                    }

                    break;

                case ConsoleKey.RightArrow:
                    if (cursorIndex < input.Length)
                    {
                        cursorIndex++;
                        changed = true;
                    }

                    break;

                case ConsoleKey.Home:
                    if (cursorIndex != 0)
                    {
                        cursorIndex = 0;
                        changed = true;
                    }

                    break;

                case ConsoleKey.End:
                    if (cursorIndex != input.Length)
                    {
                        cursorIndex = input.Length;
                        changed = true;
                    }

                    break;

                case ConsoleKey.Escape:
                    if (input.Length > 0)
                    {
                        input.Clear();
                        cursorIndex = 0;
                        changed = true;
                    }

                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        input.Insert(cursorIndex, key.KeyChar);
                        cursorIndex++;
                        changed = true;
                    }

                    break;
            }
        }

        return changed;
    }

    private void SubmitCurrentInput()
    {
        var command = input.ToString();

        if (!string.IsNullOrWhiteSpace(command))
        {
            // Echo the submitted command into the output history.
            history.Add($"{prompt}{command}");
            TrimHistory();

            commands.Writer.TryWrite(command);
        }

        input.Clear();
        cursorIndex = 0;
    }

    private void TrimHistory()
    {
        var extraLines = history.Count - maximumHistoryLines;

        if (extraLines > 0)
            history.RemoveRange(0, extraLines);
    }

    private void Render()
    {
        var width = SafeWindowWidth();
        var height = SafeWindowHeight();

        /*
         * Avoid writing into the console's final physical column.
         * In some terminal hosts that causes an automatic line wrap
         * and can scroll the whole display.
         */
        var usableWidth = Math.Max(1, width - 1);

        // The final row is reserved for input.
        var outputHeight = Math.Max(0, height - 1);

        Console.CursorVisible = false;

        var wrappedOutput = BuildVisibleOutput(
            usableWidth,
            outputHeight);
        wrappedOutput.Reverse();

        for (var row = 0; row < outputHeight; row++)
        {
            var text = row < wrappedOutput.Count
                ? wrappedOutput[row]
                : string.Empty;

            WriteRow(outputHeight - row - 1, text, usableWidth);
        }

        RenderInputLine(
            row: height - 1,
            usableWidth);

        Console.CursorVisible = true;
    }

    private List<string> BuildVisibleOutput(
        int usableWidth,
        int outputHeight)
    {
        if (outputHeight == 0)
            return [];

        var wrapped = new List<string>();

        foreach (var historyLine in history)
        {
            WrapLine(historyLine, usableWidth, wrapped);
        }

        if (wrapped.Count <= outputHeight)
            return wrapped;

        return wrapped.GetRange(
            wrapped.Count - outputHeight,
            outputHeight);
    }

    private static void WrapLine(
        string line,
        int width,
        List<string> destination)
    {
        if (line.Length == 0)
        {
            destination.Add(string.Empty);
            return;
        }

        var index = 0;

        while (index < line.Length)
        {
            var length = Math.Min(width, line.Length - index);

            destination.Add(
                line.Substring(index, length));

            index += length;
        }
    }

    private void RenderInputLine(int row, int usableWidth)
    {
        if (row < 0)
            return;

        var completeInput = prompt + input;
        var absoluteCursorPosition = prompt.Length + cursorIndex;

        /*
         * Horizontally scroll the input when it exceeds the width.
         * Keep the cursor visible near the right side.
         */
        var horizontalOffset = 0;

        if (absoluteCursorPosition >= usableWidth)
            horizontalOffset = absoluteCursorPosition - usableWidth + 1;

        var visibleInput = completeInput.Length > horizontalOffset
            ? completeInput[horizontalOffset..]
            : string.Empty;

        if (visibleInput.Length > usableWidth)
            visibleInput = visibleInput[..usableWidth];

        var foregroundColor = Console.ForegroundColor;
        if (!isInputEnabled)
            Console.ForegroundColor = ConsoleColor.DarkGray;

        WriteRow(row, visibleInput, usableWidth);

        if (!isInputEnabled)
            Console.ForegroundColor = foregroundColor;

        var visibleCursorPosition = absoluteCursorPosition - horizontalOffset;
        visibleCursorPosition = Math.Clamp(visibleCursorPosition, 0, usableWidth - 1);

        Console.SetCursorPosition(visibleCursorPosition, row);
    }

    private static void WriteRow(int row, string text, int usableWidth)
    {
        if (text.Length > usableWidth)
            text = text[..usableWidth];

        Console.SetCursorPosition(0, row);
        Console.Write(text);

        var remaining = usableWidth - text.Length;

        if (remaining > 0)
            Console.Write(new string(' ', remaining));
    }

    private static int SafeWindowWidth()
    {
        try
        {
            return Math.Max(2, Console.WindowWidth);
        }
        catch
        {
            return 80;
        }
    }

    private static int SafeWindowHeight()
    {
        try
        {
            return Math.Max(2, Console.WindowHeight);
        }
        catch
        {
            return 25;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hasDisposed)
            return;

        hasDisposed = true;

        RequestStop();

        requests.Writer.TryComplete();
        commands.Writer.TryComplete();

        stopSource.Dispose();

        await ValueTask.CompletedTask;
    }

    private abstract record TerminalRequest;

    private sealed record AppendLineRequest(string Message) : TerminalRequest;

    private sealed record ClearRequest : TerminalRequest
    {
        public static ClearRequest Instance { get; } = new();
    }

    private sealed record SetPromptRequest(string Prompt) : TerminalRequest;

    private sealed record SetInputEnabledRequest(bool Enabled) : TerminalRequest;
}