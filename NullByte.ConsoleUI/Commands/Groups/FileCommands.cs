using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class FileCommands(
    VirtualFileSystem fileSystem,
    NetworkService networkService,
    WorkingDirectory workingDirectory,
    IGameTerminal terminal) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
        registry.Register(
            new CommandDefinition(
                "ls",
                "ls [directory]",
                "List the files and directories in a directory.",
                Guard(ListAsync))
            {
                MinArgs = 0, MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Directory }]
            });

        registry.Register(
            new CommandDefinition(
                "cd",
                "cd [directory]",
                "Change the current directory. Without an argument, go to the root.",
                Guard(ChangeDirectoryAsync))
            {
                MinArgs = 0, MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Directory }]
            });

        registry.Register(
            new CommandDefinition(
                "pwd",
                "pwd",
                "Show the current directory.",
                Guard(PrintDirectoryAsync))
            { MinArgs = 0, MaxArgs = 0 });

        registry.Register(
            new CommandDefinition(
                "cat",
                "cat <filename>",
                "Display a file's contents.",
                Guard(CatAsync))
            {
                MinArgs = 1, MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Filename }]
            });

        registry.Register(
            new CommandDefinition(
                "rm",
                "rm [-r] <path>",
                "Delete a file, or with -r a directory and everything in it.",
                Guard(RemoveAsync))
            {
                MinArgs = 1, MaxArgs = 2,
                Completions = [new ArgumentCompletion { ArgumentIndex = -1, Kind = CompletionKind.FilenameOrDirectory }]
            });

        registry.Register(
            new CommandDefinition(
                "mkdir",
                "mkdir <directory>",
                "Create a directory, and any missing parents.",
                Guard(MakeDirectoryAsync))
            {
                MinArgs = 1, MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Directory }]
            });

        registry.Register(
            new CommandDefinition(
                "rmdir",
                "rmdir <directory>",
                "Delete an empty directory.",
                Guard(RemoveDirectoryAsync))
            {
                MinArgs = 1, MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Directory }]
            });

        registry.Register(
           new CommandDefinition(
               "mv",
               "mv <source> <destination>",
               "Move or rename a file or directory.",
               Guard(MoveAsync))
           {
               MinArgs = 2, MaxArgs = 2,
               Completions = [
                   new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.FilenameOrDirectory},
                   new ArgumentCompletion {ArgumentIndex = 1, Kind = CompletionKind.FilenameOrDirectory}]
           });

        registry.Register(
            new CommandDefinition(
                "scp",
                "scp <file...> <hostname>",
                "Copies files to a destination server.",
                Guard(CopyFilesAsync))
            {
                MinArgs = 2, MaxArgs = int.MaxValue,
                Completions = [new ArgumentCompletion { ArgumentIndex = -1, Kind = CompletionKind.FilenameOrKnownServer }]
            });

        registry.Register(
            new CommandDefinition(
                "edit",
                "edit <filename>",
                "Create or replace a file.",
                Guard(EditFileAsync))
            {
                MinArgs = 1, MaxArgs = 1,
                Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Filename }]
            });
    }

    // ---- navigating ----

    private Task ChangeDirectoryAsync(IReadOnlyList<string> arguments)
    {
        var typed = arguments.Count > 0 ? arguments[0] : "/";

        if (!workingDirectory.TryChange(typed, out var error))
            terminal.WriteLine(error);

        return Task.CompletedTask;
    }

    private Task PrintDirectoryAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine("/" + workingDirectory.Current);
        return Task.CompletedTask;
    }

    // ---- listing and reading ----

    private Task ListAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var typed = arguments.Count > 0 ? arguments[0] : "";
        if (!TryResolve(typed, out var directory))
            return Task.CompletedTask;

        if (!fileSystem.TryListDirectory(server.Hostname, directory, out var entries))
        {
            terminal.WriteLine(fileSystem.TryReadFile(server.Hostname, directory, out _)
                ? directory   // a file lists as itself
                : $"Directory not found: {(typed.Length > 0 ? typed : "/" + directory)}");

            return Task.CompletedTask;
        }

        if (entries.Count == 0)
        {
            terminal.WriteLine(directory.Length == 0 ? "No files on this server." : "The directory is empty.");
            return Task.CompletedTask;
        }

        foreach (var entry in entries)
            terminal.WriteLine(entry.Kind == FileSystemEntryKind.Directory ? entry.Name + "/" : entry.Name);

        return Task.CompletedTask;
    }

    private Task CatAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var typed = arguments[0];
        if (!TryResolveFile(typed, out var path))
            return Task.CompletedTask;

        if (fileSystem.TryReadFile(server.Hostname, path, out var content))
            terminal.WriteLine(content!);
        else if (fileSystem.DirectoryExists(server.Hostname, path))
            terminal.WriteLine($"{typed} is a directory.");
        else
            terminal.WriteLine($"File not found: {typed}");

        return Task.CompletedTask;
    }

    // ---- creating and deleting ----

    private Task MakeDirectoryAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        if (!TryResolve(arguments[0], out var path))
            return Task.CompletedTask;

        if (path.Length == 0)
            terminal.WriteLine("The root directory already exists.");
        else if (fileSystem.TryReadFile(server.Hostname, path, out _))
            terminal.WriteLine($"A file with that name already exists: {path}");
        else if (!fileSystem.CreateDirectory(server.Hostname, path))
            terminal.WriteLine($"Directory already exists: {path}");
        else
            terminal.WriteLine($"Created {path}/ on {server.Hostname}.");

        return Task.CompletedTask;
    }

    private Task RemoveDirectoryAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var typed = arguments[0];
        if (!TryResolve(typed, out var path))
            return Task.CompletedTask;

        if (path.Length == 0)
            terminal.WriteLine("The root directory cannot be deleted.");
        else if (fileSystem.TryReadFile(server.Hostname, path, out _))
            terminal.WriteLine($"{typed} is a file. Use 'rm' to delete it.");
        else if (!fileSystem.DirectoryExists(server.Hostname, path))
            terminal.WriteLine($"Directory not found: {typed}");
        else if (!fileSystem.DeleteDirectory(server.Hostname, path))
            terminal.WriteLine($"Directory not empty: {typed}. Use 'rm -r {typed}' to delete it and everything in it.");
        else
            terminal.WriteLine($"Deleted {path}/.");

        return Task.CompletedTask;
    }

    private Task RemoveAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var recursive = arguments.Any(IsRecursiveFlag);
        var targets = arguments.Where(argument => !IsRecursiveFlag(argument)).ToList();

        if (targets.Count != 1)
        {
            terminal.WriteLine("Usage: rm [-r] <path>");
            return Task.CompletedTask;
        }

        var typed = targets[0];
        if (!TryResolve(typed, out var path))
            return Task.CompletedTask;

        if (path.Length == 0)
        {
            terminal.WriteLine("The root directory cannot be deleted.");
            return Task.CompletedTask;
        }

        if (fileSystem.DeleteFile(server.Hostname, path))
        {
            terminal.WriteLine($"Deleted {path}.");
        }
        else if (fileSystem.DirectoryExists(server.Hostname, path))
        {
            if (!recursive)
            {
                terminal.WriteLine(
                    $"{typed} is a directory. Use 'rm -r {typed}' to delete it and everything in it, or 'rmdir' if it is empty.");
                return Task.CompletedTask;
            }

            var prefix = path + "/";
            var count = fileSystem.ListFiles(server.Hostname).Count(file => file.StartsWith(prefix, StringComparison.Ordinal));

            fileSystem.DeleteDirectory(server.Hostname, path, recursive: true);
            terminal.WriteLine(count == 0 ? $"Deleted {path}/." : $"Deleted {path}/ and the {count} file(s) in it.");
        }
        else
        {
            terminal.WriteLine($"File not found: {typed}");
        }

        return Task.CompletedTask;
    }

    // ---- moving and copying ----

    private Task MoveAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        if (!TryResolveFile(arguments[0], out var source) || !TryResolveFile(arguments[1], out var destination))
            return Task.CompletedTask;

        var isFile = fileSystem.TryReadFile(server.Hostname, source, out _);
        if (!isFile && !fileSystem.DirectoryExists(server.Hostname, source))
        {
            terminal.WriteLine($"File not found: {arguments[0]}");
            return Task.CompletedTask;
        }

        if (fileSystem.TryReadFile(server.Hostname, destination, out _))
        {
            terminal.WriteLine($"File already exists: {destination}");
            return Task.CompletedTask;
        }

        if (fileSystem.DirectoryExists(server.Hostname, destination))
        {
            terminal.WriteLine($"Directory already exists: {destination}");
            return Task.CompletedTask;
        }

        if (FileInTheWay(server.Hostname, destination) is { } blocker)
        {
            terminal.WriteLine($"{blocker} is a file, not a directory.");
            return Task.CompletedTask;
        }

        if (!isFile && destination.StartsWith(source + "/", StringComparison.Ordinal))
        {
            terminal.WriteLine("A directory cannot be moved into itself.");
            return Task.CompletedTask;
        }

        var moved = isFile
            ? fileSystem.MoveFile(server.Hostname, source, destination)
            : fileSystem.MoveDirectory(server.Hostname, source, destination);

        terminal.WriteLine(moved
            ? $"Moved {source} to {destination} on {server.Hostname}."
            : $"Could not move {source}.");

        return Task.CompletedTask;
    }

    private Task CopyFilesAsync(IReadOnlyList<string> arguments)
    {
        var source = networkService.GetCurrentServer();
        var destination = networkService.GetServer(arguments.Last());
        var files = new List<string>();

        foreach (var typed in arguments.Take(arguments.Count - 1))
        {
            if (!TryResolveFile(typed, out var file))
                return Task.CompletedTask;

            if (!fileSystem.TryReadFile(source.Hostname, file, out var content) || content == null)
            {
                terminal.WriteLine(fileSystem.DirectoryExists(source.Hostname, file)
                    ? $"{typed} is a directory; scp copies files only."
                    : $"File {typed} not found on {source.Hostname}");

                return Task.CompletedTask;
            }

            files.Add(file);
        }

        // The files keep their full path on the destination server.
        terminal.WriteLine(fileSystem.CopyFiles(source.Hostname, destination.Hostname, files)
            ? $"Files {string.Join(',', files)} copied to {destination.Hostname}"
            : "Not all files copied");

        return Task.CompletedTask;
    }

    // ---- editing ----

    private async Task EditFileAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        if (!TryResolveFile(arguments[0], out var path))
            return;

        // Check before the editor opens, so that nothing the player types is lost.
        if (fileSystem.DirectoryExists(server.Hostname, path))
        {
            terminal.WriteLine($"{arguments[0]} is a directory.");
            return;
        }

        if (FileInTheWay(server.Hostname, path) is { } blocker)
        {
            terminal.WriteLine($"{blocker} is a file, not a directory.");
            return;
        }

        var source = fileSystem.TryReadFile(server.Hostname, path, out var content) ? content! : "new file";

        terminal.SuspendProcessing();
        try
        {
            await Task.Delay(50); // We need to make sure the last update cycle of the terminal has finised

            var editor = new NanoEditor(path);
            var editedSource = await Task.Run(() => editor.Run(source));

            fileSystem.WriteFile(server.Hostname, path, editedSource);
        }
        finally
        {
            terminal.ResumeProcessing();
        }
    }

    // ---- helpers ----

    private static bool IsRecursiveFlag(string argument) => argument is "-r" or "-R";

    // What the player typed is resolved against the current directory. A bad path is explained, not thrown.
    private bool TryResolve(string typed, out string path)
    {
        if (workingDirectory.TryResolve(typed, out path, out var error))
            return true;

        terminal.WriteLine(error);
        return false;
    }

    private bool TryResolveFile(string typed, out string path)
    {
        if (workingDirectory.TryResolveFile(typed, out path, out var error))
            return true;

        terminal.WriteLine(error);
        return false;
    }

    // The first directory above the path that is really a file.
    private string? FileInTheWay(string hostname, string path)
        => VirtualPath.Ancestors(path).FirstOrDefault(ancestor => fileSystem.TryReadFile(hostname, ancestor, out _));

    // Path rules that are broken deeper down are the player's mistake, not a crash: say what is wrong.
    private Func<IReadOnlyList<string>, Task> Guard(Func<IReadOnlyList<string>, Task> handler) => async arguments =>
    {
        try
        {
            await handler(arguments);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            terminal.WriteLine(Describe(exception));
        }
    };

    private static string Describe(Exception exception)
    {
        // ArgumentException appends "(Parameter 'path')", which means nothing to a player.
        var message = exception.Message;
        var index = message.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return index >= 0 ? message[..index] : message;
    }
}