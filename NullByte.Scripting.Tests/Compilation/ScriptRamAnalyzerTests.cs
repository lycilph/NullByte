using NullByte.Scripting.Tests.Infrastructure;

namespace NullByte.Scripting.Tests.Compilation;

/// <summary>
/// A script costs a base amount of RAM plus the cost of every distinct API method it (or anything it
/// includes) calls.
/// </summary>
public sealed class ScriptRamAnalyzerTests
{
    private const string HackLibrary = """
        using NullByte.Core.Scripting;

        public static class HackLib
        {
            public static Task Run(INullByteApi nb) => nb.Hack("n00dles");
        }
        """;

    [Fact]
    public void Ram_WithoutApiCalls_IsTheBaseCost()
    {
        Assert.Equal(1.6, Ram("var value = 42;"), precision: 4);
    }

    [Fact]
    public void Ram_WithAnApiCall_AddsThatMethodsCost()
    {
        Assert.Equal(1.7, Ram("nb.Print(\"Hello\");"), precision: 4);
    }

    [Fact]
    public void Ram_WithTheSameMethodCalledRepeatedly_CountsItOnce()
    {
        Assert.Equal(1.7, Ram("nb.Print(\"One\");\nnb.Print(\"Two\");\nnb.Print(\"Three\");"), precision: 4);
    }

    [Fact]
    public void Ram_WithDistinctMethods_AddsEachOnce()
    {
        const string source = "nb.Print(\"Starting\");\nawait nb.Hack(\"n00dles\");\nawait nb.Hack(\"n00dles\");\nawait nb.Weaken(\"n00dles\");";

        Assert.Equal(5.1, Ram(source), precision: 4);
    }

    [Fact]
    public void Ram_OfHack_IsMoreThanTheBaseCost()
    {
        // Guards the include tests below against a cost of zero.
        Assert.True(HackAlone() > Ram("var x = 1;"));
    }

    [Fact]
    public void Ram_WithACallInAnInclude_CountsIt()
    {
        var viaInclude = Ram("#include \"lib.cx\"\nawait HackLib.Run(nb);", new() { ["lib.cx"] = HackLibrary });

        Assert.Equal(HackAlone(), viaInclude, precision: 6);
    }

    [Fact]
    public void Ram_WithTheSameMethodInScriptAndInclude_CountsItOnce()
    {
        var both = Ram(
            "#include \"lib.cx\"\nawait nb.Hack(\"a\");\nawait HackLib.Run(nb);",
            new() { ["lib.cx"] = HackLibrary });

        Assert.Equal(HackAlone(), both, precision: 6);
    }

    [Fact]
    public void Ram_WithAnIncludeThatIsNotCalled_StillCountsIt()
    {
        var unused = Ram("#include \"lib.cx\"\nvar x = 1;", new() { ["lib.cx"] = HackLibrary });

        Assert.Equal(HackAlone(), unused, precision: 6);
    }

    [Fact]
    public void Ram_WithNestedIncludes_CountsThem()
    {
        var nested = Ram("#include \"a.cx\"\nvar x = 1;", new()
        {
            ["a.cx"] = "#include \"b.cx\"\npublic static class A {}",
            ["b.cx"] = HackLibrary
        });

        Assert.Equal(HackAlone(), nested, precision: 6);
    }

    private static double HackAlone() => Ram("await nb.Hack(\"n00dles\");");

    private static double Ram(string source, Dictionary<string, string>? files = null)
        => CompileAssert.Succeeds(TestScripts.Compile(source, "main.cx", files ?? [])).RamCost;
}
