using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Tests.Infrastructure;

internal static class CompileAssert
{
    /// <summary>Asserts that the compilation succeeded and returns the compiled script.</summary>
    public static CompiledScript Succeeds(ScriptCompilationResult result)
    {
        Assert.True(result.Succeeded, Format(result));

        return Assert.IsType<CompiledScript>(result.Script);
    }

    /// <summary>Asserts that the compilation failed with the given diagnostic and returns that diagnostic.</summary>
    public static ScriptDiagnostic FailsWith(ScriptCompilationResult result, string diagnosticId)
    {
        Assert.False(result.Succeeded, "The compilation succeeded, but was expected to fail.");
        Assert.Null(result.Script);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == diagnosticId);

        Assert.True(diagnostic is not null, $"Expected {diagnosticId}, but got:{Environment.NewLine}{Format(result)}");

        return diagnostic!;
    }

    public static string Format(ScriptCompilationResult result)
        => string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(d => $"{d.Id} ({d.Line},{d.Column}): {d.Message}"));
}
