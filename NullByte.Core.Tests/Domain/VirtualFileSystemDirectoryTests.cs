using NullByte.Core.Domain;
using static NullByte.Core.Tests.Domain.FileSystemTestSupport;

namespace NullByte.Core.Tests.Domain;

public class VirtualFileSystemDirectoryTests
{
    // ---- creating ----

    [Fact]
    public void Creating_a_directory_reports_whether_it_is_new()
    {
        var (files, _) = Create();

        Assert.True(files.CreateDirectory("home", "scripts"));
        Assert.False(files.CreateDirectory("home", "scripts"));

        Assert.True(files.DirectoryExists("home", "scripts"));
        Assert.Equal(new[] { "scripts" }, files.ListDirectories("home"));
    }

    [Fact]
    public void Creating_a_nested_directory_creates_the_missing_parents()
    {
        var (files, _) = Create();
        var changes = Record(files);

        Assert.True(files.CreateDirectory("home", "a/b/c"));

        Assert.Equal(new[] { "a", "a/b", "a/b/c" }, files.ListDirectories("home"));
        Assert.Equal(
            new[]
            {
                new FileChange("home", "a", FileChangeKind.Created, IsDirectory: true),
                new FileChange("home", "a/b", FileChangeKind.Created, IsDirectory: true),
                new FileChange("home", "a/b/c", FileChangeKind.Created, IsDirectory: true),
            },
            changes);
    }

    [Fact]
    public void A_directory_cannot_take_the_name_of_a_file_or_live_below_one()
    {
        var (files, _) = Create();
        files.WriteFile("home", "a.cx", "x");

        Assert.Throws<InvalidOperationException>(() => files.CreateDirectory("home", "a.cx"));
        Assert.Throws<InvalidOperationException>(() => files.CreateDirectory("home", "a.cx/sub"));
        Assert.Empty(files.ListDirectories("home"));
    }

    [Fact]
    public void Writing_a_file_creates_its_parent_directories()
    {
        var (files, _) = Create();
        var changes = Record(files);

        files.WriteFile("home", "tools/net/scan.cx", "x");

        Assert.Equal(["tools", "tools/net"], files.ListDirectories("home"));
        Assert.Equal(["tools/net/scan.cx"], files.ListFiles("home"));
        Assert.Equal(
            new[]
            {
                new FileChange("home", "tools", FileChangeKind.Created, true),
                new FileChange("home", "tools/net", FileChangeKind.Created, true),
                new FileChange("home", "tools/net/scan.cx", FileChangeKind.Created),
            },
            changes);
    }

    [Fact]
    public void A_file_cannot_replace_a_directory_or_live_below_a_file()
    {
        var (files, _) = Create();
        files.CreateDirectory("home", "dir");
        files.WriteFile("home", "a.cx", "x");

        Assert.Throws<InvalidOperationException>(() => files.WriteFile("home", "dir", "x"));
        Assert.Throws<InvalidOperationException>(() => files.WriteFile("home", "a.cx/b.cx", "x"));
        Assert.Equal(new[] { "a.cx" }, files.ListFiles("home"));
    }

    [Fact]
    public void A_directory_survives_deleting_its_last_file()
    {
        var (files, _) = Create();
        files.WriteFile("home", "dir/a.cx", "x");

        files.DeleteFile("home", "dir/a.cx");

        Assert.True(files.DirectoryExists("home", "dir"));
    }

    // ---- paths ----

    [Fact]
    public void A_leading_slash_is_accepted_and_removed()
    {
        var (files, _) = Create();

        files.WriteFile("home", "/a/b.cx", "x");

        Assert.True(files.TryReadFile("home", "a/b.cx", out _));
        Assert.True(files.TryReadFile("home", "/a/b.cx", out _));
        Assert.True(files.DirectoryExists("home", "/a/"));
        Assert.Equal(new[] { "a/b.cx" }, files.ListFiles("home"));
    }

    [Fact]
    public void Paths_are_case_sensitive()
    {
        var (files, _) = Create();

        files.WriteFile("home", "A/x.cx", "1");
        files.WriteFile("home", "a/x.cx", "2");

        Assert.Equal(new[] { "A", "a" }, files.ListDirectories("home"));
        Assert.Equal(2, files.ListFiles("home").Count);
    }

    // ---- listing ----

    [Fact]
    public void Files_are_listed_with_their_full_paths()
    {
        var (files, _) = Create();
        files.WriteFile("home", "b.cx", "x");
        files.WriteFile("home", "a/z.cx", "x");
        files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "a.cx", "a/z.cx", "b.cx" }, files.ListFiles("home"));
    }

    [Fact]
    public void Listing_a_directory_shows_its_direct_children_with_directories_first()
    {
        var (files, _) = Create();
        files.WriteFile("home", "z.cx", "x");
        files.WriteFile("home", "a/deep/x.cx", "x");
        files.WriteFile("home", "a/b.cx", "x");
        files.CreateDirectory("home", "m");

        Assert.True(files.TryListDirectory("home", "", out var root));
        Assert.Equal(
            new[]
            {
                new FileSystemEntry("a", FileSystemEntryKind.Directory),
                new FileSystemEntry("m", FileSystemEntryKind.Directory),
                new FileSystemEntry("z.cx", FileSystemEntryKind.File),
            },
            root);

        Assert.True(files.TryListDirectory("home", "a", out var inner));
        Assert.Equal(
            new[]
            {
                new FileSystemEntry("deep", FileSystemEntryKind.Directory),
                new FileSystemEntry("b.cx", FileSystemEntryKind.File),
            },
            inner);

        Assert.False(files.TryListDirectory("home", "nope", out _));
    }

    // ---- deleting ----

    [Fact]
    public void An_empty_directory_is_deleted_with_one_event()
    {
        var (files, _) = Create();
        files.CreateDirectory("home", "dir");
        var changes = Record(files);

        Assert.True(files.DeleteDirectory("home", "dir"));

        Assert.False(files.DirectoryExists("home", "dir"));
        Assert.Equal(new[] { new FileChange("home", "dir", FileChangeKind.Deleted, true) }, changes);
    }

    [Fact]
    public void A_directory_with_content_needs_the_recursive_flag()
    {
        var (files, _) = Create();
        files.WriteFile("home", "d/sub/a.cx", "x");
        var changes = Record(files);

        Assert.False(files.DeleteDirectory("home", "d"));
        Assert.Empty(changes);
        Assert.True(files.DirectoryExists("home", "d"));

        Assert.True(files.DeleteDirectory("home", "d", recursive: true));

        Assert.Empty(files.ListFiles("home"));
        Assert.Empty(files.ListDirectories("home"));
        Assert.Equal(new[] { new FileChange("home", "d", FileChangeKind.Deleted, true) }, changes);   // one event for the subtree
    }

    [Fact]
    public void Missing_directories_and_the_root_cannot_be_deleted()
    {
        var (files, _) = Create();

        Assert.False(files.DeleteDirectory("home", "nope"));
        Assert.Throws<ArgumentException>(() => files.DeleteDirectory("home", "/"));
    }

    // ---- moving ----

    [Fact]
    public void Moving_a_file_into_a_new_directory_creates_it_and_keeps_the_times()
    {
        var (files, clock) = Create();
        files.WriteFile("home", "a.cx", "x");
        files.TryGetFileTimes("home", "a.cx", out var before);
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.True(files.MoveFile("home", "a.cx", "archive/old/a.cx"));

        Assert.True(files.DirectoryExists("home", "archive/old"));
        Assert.True(files.TryGetFileTimes("home", "archive/old/a.cx", out var after));
        Assert.Equal(before, after);
    }

    [Fact]
    public void A_file_cannot_be_moved_onto_a_directory_or_below_a_file()
    {
        var (files, _) = Create();
        files.WriteFile("home", "a.cx", "x");
        files.WriteFile("home", "b.cx", "y");
        files.CreateDirectory("home", "dir");

        Assert.False(files.MoveFile("home", "a.cx", "dir"));
        Assert.False(files.MoveFile("home", "a.cx", "b.cx/a.cx"));
        Assert.Equal(new[] { "a.cx", "b.cx" }, files.ListFiles("home"));
    }

    [Fact]
    public void Moving_a_directory_moves_everything_below_it()
    {
        var (files, clock) = Create();
        files.WriteFile("home", "old/a.cx", "x");
        files.WriteFile("home", "old/sub/b.cx", "y");
        files.CreateDirectory("home", "old/empty");
        files.TryGetFileTimes("home", "old/sub/b.cx", out var before);
        clock.Advance(TimeSpan.FromMinutes(1));
        var changes = Record(files);

        Assert.True(files.MoveDirectory("home", "old", "new"));

        Assert.Equal(new[] { "new/a.cx", "new/sub/b.cx" }, files.ListFiles("home"));
        Assert.Equal(new[] { "new", "new/empty", "new/sub" }, files.ListDirectories("home"));
        Assert.True(files.TryGetFileTimes("home", "new/sub/b.cx", out var after));
        Assert.Equal(before, after);
        Assert.Equal(
            new[]
            {
                new FileChange("home", "old", FileChangeKind.Deleted, true),
                new FileChange("home", "new", FileChangeKind.Created, true),
            },
            changes);
    }

    [Fact]
    public void A_directory_cannot_move_into_itself_or_onto_something_that_exists()
    {
        var (files, _) = Create();
        files.WriteFile("home", "a/x.cx", "x");
        files.WriteFile("home", "b.cx", "y");
        files.CreateDirectory("home", "c");

        Assert.False(files.MoveDirectory("home", "a", "a/inner"));
        Assert.False(files.MoveDirectory("home", "a", "c"));
        Assert.False(files.MoveDirectory("home", "a", "b.cx"));
        Assert.False(files.MoveDirectory("home", "missing", "z"));
        Assert.True(files.MoveDirectory("home", "a", "a"));   // the same path: nothing to do

        Assert.Equal(new[] { "a", "c" }, files.ListDirectories("home"));
    }

    [Fact]
    public void Moving_a_directory_too_deep_changes_nothing()
    {
        var (files, _) = Create();
        files.WriteFile("home", "a/b/c/d/e/f/g.cx", "x");   // 7 levels

        // The file would end up at x/y/z/b/c/d/e/f/g.cx: 9 levels.
        Assert.Throws<ArgumentException>(() => files.MoveDirectory("home", "a", "x/y/z"));

        Assert.Equal(new[] { "a/b/c/d/e/f/g.cx" }, files.ListFiles("home"));
        Assert.False(files.DirectoryExists("home", "x"));
    }

    // ---- copying ----

    [Fact]
    public void Copying_creates_the_directories_on_the_destination()
    {
        var (files, _) = Create();
        files.WriteFile("home", "tools/scan.cx", "x");

        Assert.True(files.CopyFiles("home", "n00dles", new[] { "tools/scan.cx" }));

        Assert.True(files.TryReadFile("n00dles", "tools/scan.cx", out var content));
        Assert.Equal("x", content);
        Assert.True(files.DirectoryExists("n00dles", "tools"));
    }

    [Fact]
    public void A_copy_conflict_reports_failure_but_the_other_files_are_still_copied()
    {
        var (files, _) = Create();
        files.WriteFile("home", "a.cx", "x");
        files.WriteFile("home", "b.cx", "y");
        files.CreateDirectory("n00dles", "a.cx");   // a directory with that name is in the way

        Assert.False(files.CopyFiles("home", "n00dles", new[] { "a.cx", "b.cx" }));

        Assert.True(files.TryReadFile("n00dles", "b.cx", out _));
    }
}