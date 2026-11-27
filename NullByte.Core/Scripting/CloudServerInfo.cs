namespace NullByte.Core.Scripting;

/// <summary>Read-only information about an owned cloud server.</summary>
public sealed record CloudServerInfo(string Hostname, double MaximumRam, double UsedRam);
