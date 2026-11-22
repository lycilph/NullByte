using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Compilation;

public sealed class IncludeRamTests
{
    private const string HackLib = """
        using NullByte.Core.Scripting;

        public static class HackLib
        {
            public static Task Run(INullByteApi nb) => nb.Hack("n00dles");
        }
        """;

    private static double Ram(string main, Dictionary<string, string>? files = null)
    {
        var result = new ScriptCompiler().Compile(main, "main.cx", new FakeSources(files ?? []));
        Assert.True(result.Succeeded, string.Join("\n", result.Diagnostics));
        return result.Script!.RamCost;
    }

    private static readonly double Alone = Ram("await nb.Hack(\"n00dles\");");

    [Fact]
    public void Baseline_HackCostsSomething()
        => Assert.True(Alone > Ram("var x = 1;"));   // guards the tests below against a zero cost

    [Fact]
    public void CallsInAnIncludeAreCounted()
    {
        var viaInclude = Ram("#include \"lib.cx\"\nawait HackLib.Run(nb);", new() { ["lib.cx"] = HackLib });
        Assert.Equal(Alone, viaInclude, precision: 6);
    }

    [Fact]
    public void SameMethodInScriptAndInclude_IsCountedOnce()
    {
        var both = Ram(
            "#include \"lib.cx\"\nawait nb.Hack(\"a\");\nawait HackLib.Run(nb);",
            new() { ["lib.cx"] = HackLib });
        Assert.Equal(Alone, both, precision: 6);
    }

    [Fact]
    public void UnusedFunctionsInAnIncludeStillCost()
    {
        var unused = Ram("#include \"lib.cx\"\nvar x = 1;", new() { ["lib.cx"] = HackLib });
        Assert.Equal(Alone, unused, precision: 6);
    }

    [Fact]
    public void NestedIncludesAreCounted()
    {
        var nested = Ram("#include \"a.cx\"\nvar x = 1;", new()
        {
            ["a.cx"] = "#include \"b.cx\"\npublic static class A {}",
            ["b.cx"] = HackLib
        });
        Assert.Equal(Alone, nested, precision: 6);
    }
}