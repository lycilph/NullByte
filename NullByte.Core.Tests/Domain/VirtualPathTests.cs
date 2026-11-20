using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public class VirtualPathTests
{
    [Theory]
    [InlineData("a.cx", "a.cx")]
    [InlineData("/a.cx", "a.cx")]
    [InlineData("a/b/c.cx", "a/b/c.cx")]
    [InlineData("/a/b/", "a/b")]
    [InlineData("name with spaces.cx", "name with spaces.cx")]
    public void Valid_paths_are_normalized(string input, string expected)
        => Assert.Equal(expected, VirtualPath.NormalizeDirectory(input));

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void The_root_is_the_empty_path(string input)
        => Assert.Equal("", VirtualPath.NormalizeDirectory(input));

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void A_file_path_cannot_be_the_root(string input)
        => Assert.Throws<ArgumentException>(() => VirtualPath.NormalizeFile(input));

    [Theory]
    [InlineData("a//b")]
    [InlineData("a/./b")]
    [InlineData("a/../b")]
    [InlineData("..")]
    [InlineData("a\\b")]
    [InlineData("   ")]
    [InlineData("a/ /b")]
    [InlineData("a\tb")]
    public void Invalid_paths_are_rejected(string input)
        => Assert.Throws<ArgumentException>(() => VirtualPath.NormalizeFile(input));

    [Fact]
    public void Depth_is_limited()
    {
        var allowed = string.Join('/', Enumerable.Repeat("d", VirtualPath.MaxDepth));

        Assert.Equal(allowed, VirtualPath.NormalizeFile(allowed));
        Assert.Throws<ArgumentException>(() => VirtualPath.NormalizeFile(allowed + "/d"));
    }

    [Fact]
    public void Length_is_limited()
    {
        var allowed = new string('a', VirtualPath.MaxLength);

        Assert.Equal(allowed, VirtualPath.NormalizeFile(allowed));
        Assert.Throws<ArgumentException>(() => VirtualPath.NormalizeFile(allowed + "a"));
    }

    [Fact]
    public void Parent_name_and_ancestors_split_a_path()
    {
        Assert.Equal("a/b", VirtualPath.Parent("a/b/c.cx"));
        Assert.Equal("", VirtualPath.Parent("a"));
        Assert.Null(VirtualPath.Parent(""));

        Assert.Equal("c.cx", VirtualPath.Name("a/b/c.cx"));
        Assert.Equal("c.cx", VirtualPath.Name("c.cx"));

        Assert.Equal(new[] { "a", "a/b" }, VirtualPath.Ancestors("a/b/c.cx"));
        Assert.Empty(VirtualPath.Ancestors("c.cx"));
    }
}