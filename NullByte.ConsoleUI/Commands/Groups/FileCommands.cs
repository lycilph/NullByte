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
               "mv",
               "mv <source> <destination>",
               "Moves a file on a server.",
               MoveFileAsync)
           { 
               MinArgs = 2, MaxArgs = 2,
               Completions = [
                   new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename},
                   new ArgumentCompletion {ArgumentIndex = 1, Kind = CompletionKind.Filename}]
           });

        registry.Register(
            new CommandDefinition(
                "scp",
                "scp <file...> <hostname>",
                "Copies files to a destination server.",
                CopyFilesAsync)
            { 
                MinArgs = 2, MaxArgs = int.MaxValue,
                Completions = [new ArgumentCompletion { ArgumentIndex = -1, Kind = CompletionKind.FilenameOrKnownServer }]
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

    private async Task MoveFileAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var sourceFilename = arguments[0];
        var destinationFilename = arguments[1];

        if (!fileSystem.TryReadFile(server.Hostname, sourceFilename, out _))
        {
            terminal.WriteLine($"File not found: {sourceFilename}");
            return;
        }

        if (fileSystem.TryReadFile(server.Hostname, destinationFilename, out _))
        {
            terminal.WriteLine($"File already exists: {destinationFilename}");
            return;
        }

        if (!fileSystem.MoveFile(server.Hostname, sourceFilename, destinationFilename))
        {
            terminal.WriteLine($"Could not rename {sourceFilename}.");
            return;
        }

        terminal.WriteLine($"Moved {sourceFilename} to {destinationFilename} on {server.Hostname}.");
    }

    private async Task CopyFilesAsync(IReadOnlyList<string> arguments)
    {
        var source = networkService.GetCurrentServer();
        var destination = networkService.GetServer(arguments.Last());
        var files = arguments.Take(arguments.Count - 1).ToList();

        foreach (var file in files)
        {
            if (!fileSystem.TryReadFile(source.Hostname, file, out string? content) || content == null)
            {
                terminal.WriteLine($"File {file} not found on {source.Hostname}");
                return;
            }
        }

        if (fileSystem.CopyFiles(source.Hostname, destination.Hostname, files))
            terminal.WriteLine($"Files {string.Join(',', files)} copied to {destination.Hostname}");
        else
            terminal.WriteLine($"Not all files copied");
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

        var editor = new NanoEditor(filename);
        var edited_source = await Task.Run(() => editor.Run(source));

        fileSystem.WriteFile(server.Hostname, filename, edited_source);
        terminal.ResumeProcessing();
    }
}