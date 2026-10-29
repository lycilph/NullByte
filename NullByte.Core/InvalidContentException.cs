namespace NullByte.Core;

public sealed class InvalidContentException(string message) : Exception(message) {}