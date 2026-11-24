namespace NullByte.Core.Scripting.Activities;

/// <summary>
/// The outcome of asking for an activity to start from a script.
/// </summary>
/// <param name="Succeeded">Whether the activity was started.</param>
/// <param name="Message">What happened: how it started (for example <c>Hired as Intern.</c>), or why it could not.</param>
public sealed record ActivityActionResult(bool Succeeded, string Message);