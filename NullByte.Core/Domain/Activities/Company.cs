namespace NullByte.Core.Domain;

/// <summary>
/// An employer offering one or more career paths.
/// </summary>
/// <param name="Name">Unique command-friendly name, e.g. <c>pixelworks</c>.</param>
/// <param name="Careers">The career paths on offer. Never empty.</param>
public sealed record Company(string Name, IReadOnlyList<CareerPath> Careers);