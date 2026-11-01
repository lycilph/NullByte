namespace NullByte.Scripting;

/// <summary>
/// The globals object passed to every script. Scripts reference it as `api`.
/// </summary>
 
public sealed class ScriptGlobals
{
#pragma warning disable IDE1006 // Naming Styles
    public NullbyteApi api { get; }
#pragma warning restore IDE1006 // Naming Styles

    internal ScriptGlobals(NullbyteApi api) {  this.api = api; }
}
