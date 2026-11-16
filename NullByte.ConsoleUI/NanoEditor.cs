using System.Text;

namespace NullByte.ConsoleUI;

public class NanoEditor
{
    private readonly string filename;
    private readonly List<string> lines;
    private int cursorX = 0;
    private int cursorY = 0;
    private int scrollOffsetY = 0;
    private int scrollOffsetX = 0;
    private bool isDirty = false;
    private string statusMessage = "";

    public NanoEditor(string filename, string initialContent)
    {
        this.filename = filename;
        // Normalize line endings and split into editable lines
        var normalized = (initialContent ?? string.Empty).Replace("\r\n", "\n");
        lines = new List<string>(normalized.Split('\n'));
        if (lines.Count == 0) lines.Add(string.Empty);
    }

    /// <summary>
    /// Opens the editor loop and returns the final saved content (or original content if discarded).
    /// </summary>
    public string Run(string originalContent)
    {
        bool prevCursorVisible = OperatingSystem.IsWindows() ? Console.CursorVisible : true;
        Console.CursorVisible = true;

        // Clear screen once on entry
        Console.Write("\u001b[2J\u001b[H");

        while (true)
        {
            AdjustScroll();
            Render();

            var keyInfo = Console.ReadKey(intercept: true);

            // Handle Ctrl shortcuts
            if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
            {
                if (keyInfo.Key == ConsoleKey.O)
                {
                    originalContent = string.Join("\n", lines);
                    isDirty = false;
                    statusMessage = $"[ Wrote {lines.Count} lines to {filename} ]";
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.C)
                {
                    statusMessage = "Clear entire file? (Y)es / (N)o";
                    Render();

                    var confirm = Console.ReadKey(intercept: true);
                    if (confirm.Key == ConsoleKey.Y)
                    {
                        lines.Clear();
                        lines.Add(string.Empty);
                        cursorX = 0;
                        cursorY = 0;
                        scrollOffsetX = 0;
                        scrollOffsetY = 0;
                        isDirty = true;
                        statusMessage = "[ Buffer cleared ]";
                    }
                    else
                    {
                        statusMessage = "[ Cancelled ]";
                    }
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.X)
                {
                    if (!isDirty)
                    {
                        CleanupAndExit(prevCursorVisible);
                        return originalContent;
                    }

                    // Prompt to save modified buffer (Nano style)
                    statusMessage = "Save modified buffer? (Y)es / (N)o / (C)ancel";
                    Render();
                    var confirm = Console.ReadKey(intercept: true);
                    if (confirm.Key == ConsoleKey.Y)
                    {
                        CleanupAndExit(prevCursorVisible);
                        return string.Join("\n", lines);
                    }
                    if (confirm.Key == ConsoleKey.N)
                    {
                        CleanupAndExit(prevCursorVisible);
                        return originalContent;
                    }
                    statusMessage = "[ Cancelled ]";
                    continue;
                }
            }

            statusMessage = string.Empty;
            HandleInput(keyInfo);
        }
    }

    private void HandleInput(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                if (cursorY > 0)
                {
                    cursorY--;
                    cursorX = Math.Min(cursorX, lines[cursorY].Length);
                }
                break;

            case ConsoleKey.DownArrow:
                if (cursorY < lines.Count - 1)
                {
                    cursorY++;
                    cursorX = Math.Min(cursorX, lines[cursorY].Length);
                }
                break;

            case ConsoleKey.LeftArrow:
                if (cursorX > 0)
                {
                    cursorX--;
                }
                else if (cursorY > 0)
                {
                    cursorY--;
                    cursorX = lines[cursorY].Length;
                }
                break;

            case ConsoleKey.RightArrow:
                if (cursorX < lines[cursorY].Length)
                {
                    cursorX++;
                }
                else if (cursorY < lines.Count - 1)
                {
                    cursorY++;
                    cursorX = 0;
                }
                break;

            case ConsoleKey.Home:
                cursorX = 0;
                break;

            case ConsoleKey.End:
                cursorX = lines[cursorY].Length;
                break;

            case ConsoleKey.Enter:
                string currentLine = lines[cursorY];
                string remainder = currentLine.Substring(cursorX);
                lines[cursorY] = currentLine.Substring(0, cursorX);
                lines.Insert(cursorY + 1, remainder);
                cursorY++;
                cursorX = 0;
                isDirty = true;
                break;

            case ConsoleKey.Backspace:
                if (cursorX > 0)
                {
                    lines[cursorY] = lines[cursorY].Remove(cursorX - 1, 1);
                    cursorX--;
                    isDirty = true;
                }
                else if (cursorY > 0)
                {
                    string lineToMerge = lines[cursorY];
                    lines.RemoveAt(cursorY);
                    cursorY--;
                    cursorX = lines[cursorY].Length;
                    lines[cursorY] += lineToMerge;
                    isDirty = true;
                }
                break;

            case ConsoleKey.Delete:
                if (cursorX < lines[cursorY].Length)
                {
                    lines[cursorY] = lines[cursorY].Remove(cursorX, 1);
                    isDirty = true;
                }
                else if (cursorY < lines.Count - 1)
                {
                    lines[cursorY] += lines[cursorY + 1];
                    lines.RemoveAt(cursorY + 1);
                    isDirty = true;
                }
                break;

            case ConsoleKey.Tab:
                // Insert 2 spaces for clean script indentation
                lines[cursorY] = lines[cursorY].Insert(cursorX, "  ");
                cursorX += 2;
                isDirty = true;
                break;

            default:
                if (!char.IsControl(keyInfo.KeyChar))
                {
                    lines[cursorY] = lines[cursorY].Insert(cursorX, keyInfo.KeyChar.ToString());
                    cursorX++;
                    isDirty = true;
                }
                break;
        }
    }

    private void AdjustScroll()
    {
        int editHeight = Math.Max(1, Console.WindowHeight - 4); // Reserve 1 top bar + 3 bottom bars
        int editWidth = Math.Max(1, Console.WindowWidth);

        if (cursorY < scrollOffsetY) scrollOffsetY = cursorY;
        if (cursorY >= scrollOffsetY + editHeight) scrollOffsetY = cursorY - editHeight + 1;

        if (cursorX < scrollOffsetX) scrollOffsetX = cursorX;
        if (cursorX >= scrollOffsetX + editWidth) scrollOffsetX = cursorX - editWidth + 1;
    }

    private void Render()
    {
        int width = Math.Max(20, Console.WindowWidth);
        int height = Math.Max(6, Console.WindowHeight);
        int editHeight = height - 4;

        var sb = new StringBuilder();
        // Move cursor to top-left without clearing screen (prevents flicker)
        sb.Append("\u001b[H");

        // 1. Top Header Bar (Inverted colors)
        string modifiedTag = isDirty ? "Modified" : "        ";
        string title = $"  NullByte nano 7.2 (BitOS)     File: {filename}";
        if (title.Length > width - 10) title = title.Substring(0, width - 10);
        string header = title.PadRight(width - modifiedTag.Length) + modifiedTag;
        sb.Append($"\u001b[7m{header}\u001b[0m\n");

        // 2. Text Buffer Area
        for (int i = 0; i < editHeight; i++)
        {
            int lineIndex = scrollOffsetY + i;
            string visibleText = string.Empty;

            if (lineIndex < lines.Count)
            {
                string fullLine = lines[lineIndex];
                if (scrollOffsetX < fullLine.Length)
                {
                    int take = Math.Min(width, fullLine.Length - scrollOffsetX);
                    visibleText = fullLine.Substring(scrollOffsetX, take);
                }
            }

            // Pad with spaces to overwrite old characters cleanly
            sb.Append(visibleText.PadRight(width)).Append('\n');
        }

        // 3. Status / Prompt Line
        string centeredStatus = string.IsNullOrEmpty(statusMessage)
            ? $"Line {cursorY + 1}/{lines.Count}, Col {cursorX + 1}"
            : statusMessage;
        if (centeredStatus.Length > width) centeredStatus = centeredStatus.Substring(0, width);
        sb.Append($"\u001b[7m{centeredStatus.PadRight(width)}\u001b[0m\n");

        // 4. Bottom Nano Shortcut Footer (2 rows)
        string row1 = FormatShortcut("^O", "Write Out") + FormatShortcut("^C", "Clear") + FormatShortcut("^X", "Exit");
        string row2 = FormatShortcut("Tab", "Indent (2sp)") + FormatShortcut("Arrows", "Move Cursor");
        sb.Append(row1.PadRight(width)).Append('\n');
        sb.Append(row2.PadRight(width));

        Console.Write(sb.ToString());

        // Position physical terminal cursor
        int screenX = Math.Clamp(cursorX - scrollOffsetX, 0, width - 1);
        int screenY = Math.Clamp((cursorY - scrollOffsetY) + 1, 1, editHeight);
        Console.SetCursorPosition(screenX, screenY);
    }

    private static string FormatShortcut(string key, string label)
    {
        return $"\u001b[7m{key}\u001b[0m {label,-14}";
    }

    private static void CleanupAndExit(bool restoreCursor)
    {
        Console.Write("\u001b[2J\u001b[H");
        if (OperatingSystem.IsWindows()) Console.CursorVisible = restoreCursor;
    }
}