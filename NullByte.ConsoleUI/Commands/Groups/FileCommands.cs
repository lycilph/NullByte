using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class FileCommands(VirtualFileSystem fileSystem, NetworkService networkService, IGameTerminal terminal) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
        registry.Register(
            new CommandDefinition(
                "ls",
                "ls",
                "List files.",
                ListFilesAsync));

        registry.Register(
            new CommandDefinition(
                "cat",
                "cat <filename>",
                "Display a file's contents.",
                CatAsync)
            {
                MinArgs = 1, MaxArgs = 1,
                Completions = [new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename}]
            });

        registry.Register(
            new CommandDefinition(
                "rm",
                "rm <filename>",
                "Delete a file.",
                RemoveFileAsync)
            {
                MinArgs = 1, MaxArgs = 1,
                Completions = [new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename}]
            });

        registry.Register(
            new CommandDefinition(
                "edit",
                "edit <filename>",
                "Create or replace a file.",
                EditFileAsync)
            {
                MinArgs = 1, MaxArgs = 1,
                Completions = [new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename}]
            });
    }
    private async Task ListFilesAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var files = fileSystem.ListFiles(server.Hostname);

        if (files.Count == 0)
        {
            terminal.WriteLine("No files on this server.");
            return;
        }

        foreach (var file in files)
            terminal.WriteLine(file);
    }

    private Task CatAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();

        if (!fileSystem.TryReadFile(server.Hostname, arguments[0], out var content))
        {
            terminal.WriteLine($"File not found: {arguments[0]}");

            return Task.CompletedTask;
        }

        terminal.WriteLine(content!);

        return Task.CompletedTask;
    }

    private Task RemoveFileAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var fileName = arguments[0];

        if (!fileSystem.DeleteFile(server.Hostname, fileName))
        {
            terminal.WriteLine($"File not found: {fileName}");

            return Task.CompletedTask;
        }

        terminal.WriteLine($"Deleted {fileName}.");

        return Task.CompletedTask;
    }

    private async Task EditFileAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var filename = arguments[0];
        var source =
            fileSystem.TryReadFile(server.Hostname, filename, out var content) ? content! : "new file";

        terminal.SuspendProcessing();
        await Task.Delay(50); // We need to make sure the last update cycle of the terminal has finised

        var editor = new NanoEditor(filename, source);
        var edited_source = await Task.Run(() => editor.Run(""));

        fileSystem.WriteFile(server.Hostname, filename, edited_source);
        terminal.ResumeProcessing();
    }
}