using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace NullByte.ConsoleUI.Scripting;

public sealed class ScriptRunner
{
    private readonly ScriptOptions _options;
    private readonly IGameTerminal terminal;

    public ScriptRunner(IGameTerminal terminal)
    {
        _options = ScriptOptions.Default
            .AddReferences(
                typeof(IScriptApi).Assembly)
            .AddImports(
                "System",
                "System.Threading",
                "System.Threading.Tasks");
        this.terminal = terminal;
    }

    public async Task RunAsync(
        string source,
        IScriptApi api,
        CancellationToken cancellationToken)
    {
        var globals = new ScriptGlobals
        {
            Api = api,
            CancellationToken = cancellationToken
        };

        var script = CSharpScript.Create(
            source,
            _options,
            typeof(ScriptGlobals));

        var diagnostics = script.Compile();

        var errors = diagnostics
            .Where(x =>
                x.Severity ==
                Microsoft.CodeAnalysis
                    .DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            foreach (var error in errors)
            {
                terminal.WriteLine(error.GetMessage());
            }

            return;
        }

        await script.RunAsync(
            globals,
            cancellationToken);
    }
}