namespace NullByte.Core.Scripting;

/// <summary>The outcome of a cloud-server operation requested by a script.</summary>
public sealed record CloudServerActionResult(
    bool Succeeded,
    string FailureMessage,
    double Cost,
    CloudServerInfo? Server);
