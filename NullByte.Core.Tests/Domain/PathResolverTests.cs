using NullByte.Core.Domain;

namespace NullByte.Core.Tests;

public class PathResolverTests
{
    [Theory]
    [InlineData("", "a", "a")]
    [InlineData("", "", "")]
    [InlineData("tools", "", "tools")]
    [InlineData("tools", "net", "tools/net")]
    [InlineData("tools", "net/", "tools/net")]
    [InlineData("tools", ".", "tools")]
    [InlineData("tools", "./net/../x", "tools/x")]
    [InlineData("tools/net", "..", "tools")]
    [InlineData("tools", "../lib", "lib")]
    [InlineData("tools", "/lib", "lib")]
    [InlineData("tools", "/", "")]
    [InlineData("", "..", "")]
    [InlineData("tools", "../../..", "")]
    public void Typed_paths_resolve_against_the_current_directory(string current, string typed, string expected)
    {
        Assert.True(PathResolver.TryResolve(current, typed, out var resolved, out _));
        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData("a//b")]
    [InlineData("a\\b")]
    [InlineData("a/ /b")]
    [InlineData("a\tb")]
    public void Invalid_segments_are_reported_in_plain_words(string typed)
    {
        Assert.False(PathResolver.TryResolve("tools", typed, out var resolved, out var error));
        Assert.Equal("", resolved);
        Assert.StartsWith("Path segments must be simple names", error);
    }

    [Fact]
    public void The_result_still_obeys_the_depth_limit()
    {
        var deep = string.Join('/', Enumerable.Repeat("d", VirtualPath.MaxDepth));

        Assert.True(PathResolver.TryResolve(deep, ".", out _, out _));
        Assert.False(PathResolver.TryResolve(deep, "more", out _, out var error));
        Assert.Contains("levels deep", error);
    }

    [Fact]
    public void A_file_cannot_be_the_root()
    {
        Assert.False(PathResolver.TryResolveFile("tools", "..", out _, out var error));
        Assert.Equal("A file path cannot be empty.", error);

        Assert.True(PathResolver.TryResolveFile("tools", "../a.cx", out var resolved, out _));
        Assert.Equal("a.cx", resolved);
    }
}