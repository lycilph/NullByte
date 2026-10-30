namespace NullByte.Scripting;

/// <summary>
/// Player-facing scripting API.
///
/// Queries are synchronous properties (plain reads of engine state).
/// Actions are awaitable Tasks: they submit the same ICommand the console
/// UI submits, and resolve on the game tick barrier — a script physically
/// cannot act between ticks, and can never bypass game rules.
/// </summary>

public sealed class NullbyteApi
{

}
