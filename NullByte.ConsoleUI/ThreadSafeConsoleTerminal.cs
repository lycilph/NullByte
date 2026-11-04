using System.Text;
using System.Threading.Channels;

namespace NullByte.ConsoleUI;

public sealed class ThreadSafeConsoleTerminal :
    IGameTerminal,
    IAsyncDisposable
{
    private const int RefreshDelayMilliseconds = 16;

    private readonly string _prompt;
    private readonly int _maximumHistoryLines;

    /*
     * Multiple background threads may write to this channel.
     * Only the UI loop reads from it.
     */
    private readonly Channel<TerminalRequest> _requests =
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
    private readonly Channel<string> _commands =
        Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });

    private readonly CancellationTokenSource _stopSource = new();

    // These fields are accessed only by the UI loop.
    private readonly List<string> _history = [];
    private readonly StringBuilder _input = new();

    private int _cursorIndex;
    private int _lastWindowWidth = -1;
    private int _lastWindowHeight = -1;
    private bool _screenInvalidated = true;
    private bool _hasStarted;
    private bool _hasDisposed;

    public ThreadSafeConsoleTerminal(
        string prompt = "> ",
        int maximumHistoryLines = 1_000)
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

        _prompt = prompt;
        _maximumHistoryLines = maximumHistoryLines;
    }

    public CancellationToken StoppingToken => _stopSource.Token;

    public void WriteLine()
    {
        WriteLine(string.Empty);
    }

    public void WriteLine(string message)
    {
        if (_hasDisposed || _stopSource.IsCancellationRequested)
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
            _requests.Writer.TryWrite(
                new AppendLineRequest(line));
        }
    }

    public void Clear()
    {
        if (_hasDisposed || _stopSource.IsCancellationRequested)
            return;

        _requests.Writer.TryWrite(ClearRequest.Instance);
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        if (_hasStarted)
        {
            throw new InvalidOperationException(
                "The terminal UI loop can only be started once.");
        }

        _hasStarted = true;

        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _stopSource.Token);

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

                if (currentWidth != _lastWindowWidth ||
                    currentHeight != _lastWindowHeight)
                {
                    _lastWindowWidth = currentWidth;
                    _lastWindowHeight = currentHeight;
                    changed = true;
                }

                if (changed || _screenInvalidated)
                {
                    Render();
                    _screenInvalidated = false;
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
            _commands.Writer.TryComplete();
            RestoreConsole();
        }
    }

    public IAsyncEnumerable<string> ReadCommandsAsync(
        CancellationToken cancellationToken = default)
    {
        return _commands.Reader.ReadAllAsync(cancellationToken);
    }

    public void RequestStop()
    {
        if (!_stopSource.IsCancellationRequested)
            _stopSource.Cancel();
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

        _screenInvalidated = true;
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

        while (_requests.Reader.TryRead(out var request))
        {
            switch (request)
            {
                case AppendLineRequest append:
                    _history.Add(append.Message);
                    TrimHistory();
                    changed = true;
                    break;

                case ClearRequest:
                    _history.Clear();
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

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    SubmitCurrentInput();
                    changed = true;
                    break;

                case ConsoleKey.Backspace:
                    if (_cursorIndex > 0)
                    {
                        _input.Remove(_cursorIndex - 1, 1);
                        _cursorIndex--;
                        changed = true;
                    }

                    break;

                case ConsoleKey.Delete:
                    if (_cursorIndex < _input.Length)
                    {
                        _input.Remove(_cursorIndex, 1);
                        changed = true;
                    }

                    break;

                case ConsoleKey.LeftArrow:
                    if (_cursorIndex > 0)
                    {
                        _cursorIndex--;
                        changed = true;
                    }

                    break;

                case ConsoleKey.RightArrow:
                    if (_cursorIndex < _input.Length)
                    {
                        _cursorIndex++;
                        changed = true;
                    }

                    break;

                case ConsoleKey.Home:
                    if (_cursorIndex != 0)
                    {
                        _cursorIndex = 0;
                        changed = true;
                    }

                    break;

                case ConsoleKey.End:
                    if (_cursorIndex != _input.Length)
                    {
                        _cursorIndex = _input.Length;
                        changed = true;
                    }

                    break;

                case ConsoleKey.Escape:
                    if (_input.Length > 0)
                    {
                        _input.Clear();
                        _cursorIndex = 0;
                        changed = true;
                    }

                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        _input.Insert(_cursorIndex, key.KeyChar);
                        _cursorIndex++;
                        changed = true;
                    }

                    break;
            }
        }

        return changed;
    }

    private void SubmitCurrentInput()
    {
        var command = _input.ToString();

        if (!string.IsNullOrWhiteSpace(command))
        {
            // Echo the submitted command into the output history.
            _history.Add($"{_prompt}{command}");
            TrimHistory();

            _commands.Writer.TryWrite(command);
        }

        _input.Clear();
        _cursorIndex = 0;
    }

    private void TrimHistory()
    {
        var extraLines = _history.Count - _maximumHistoryLines;

        if (extraLines > 0)
            _history.RemoveRange(0, extraLines);
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

        for (var row = 0; row < outputHeight; row++)
        {
            var text = row < wrappedOutput.Count
                ? wrappedOutput[row]
                : string.Empty;

            WriteRow(row, text, usableWidth);
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

        foreach (var historyLine in _history)
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

    private void RenderInputLine(
        int row,
        int usableWidth)
    {
        if (row < 0)
            return;

        var completeInput = _prompt + _input;
        var absoluteCursorPosition = _prompt.Length + _cursorIndex;

        /*
         * Horizontally scroll the input when it exceeds the width.
         * Keep the cursor visible near the right side.
         */
        var horizontalOffset = 0;

        if (absoluteCursorPosition >= usableWidth)
        {
            horizontalOffset =
                absoluteCursorPosition - usableWidth + 1;
        }

        var visibleInput = completeInput.Length > horizontalOffset
            ? completeInput[horizontalOffset..]
            : string.Empty;

        if (visibleInput.Length > usableWidth)
            visibleInput = visibleInput[..usableWidth];

        WriteRow(row, visibleInput, usableWidth);

        var visibleCursorPosition =
            absoluteCursorPosition - horizontalOffset;

        visibleCursorPosition = Math.Clamp(
            visibleCursorPosition,
            0,
            usableWidth - 1);

        Console.SetCursorPosition(
            visibleCursorPosition,
            row);
    }

    private static void WriteRow(
        int row,
        string text,
        int usableWidth)
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
        if (_hasDisposed)
            return;

        _hasDisposed = true;

        RequestStop();

        _requests.Writer.TryComplete();
        _commands.Writer.TryComplete();

        _stopSource.Dispose();

        await ValueTask.CompletedTask;
    }

    private abstract record TerminalRequest;

    private sealed record AppendLineRequest(
        string Message) : TerminalRequest;

    private sealed record ClearRequest : TerminalRequest
    {
        public static ClearRequest Instance { get; } = new();
    }
}