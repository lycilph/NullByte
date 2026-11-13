using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class FileCommands(VirtualFileSystem fileSystem, NetworkService networkService, IGameOutput output) : ICommandGroup
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
                "edit <filename> <source>",
                "Create or replace a file.",
                EditFileAsync)
            {
                MinArgs = 2, MaxArgs = 2,
                Completions = [new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename}]
            });
    }
    private async Task ListFilesAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var files = fileSystem.ListFiles(server.Hostname);

        if (files.Count == 0)
        {
            output.WriteLine("No files on this server.");
            return;
        }

        foreach (var file in files)
            output.WriteLine(file);
    }

    private Task CatAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();

        if (!fileSystem.TryReadFile(server.Hostname, arguments[0], out var content))
        {
            output.WriteLine($"File not found: {arguments[0]}");

            return Task.CompletedTask;
        }

        output.WriteLine(content!);

        return Task.CompletedTask;
    }

    private Task RemoveFileAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var fileName = arguments[0];

        if (!fileSystem.DeleteFile(server.Hostname, fileName))
        {
            output.WriteLine($"File not found: {fileName}");

            return Task.CompletedTask;
        }

        output.WriteLine($"Deleted {fileName}.");

        return Task.CompletedTask;
    }

    private Task EditFileAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var fileName = arguments[0];
        var source = arguments[1];

        fileSystem.WriteFile(server.Hostname, fileName, source);

        output.WriteLine($"Saved {fileName}.");

        return Task.CompletedTask;
    }
}