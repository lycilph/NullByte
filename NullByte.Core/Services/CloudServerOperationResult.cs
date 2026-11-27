using NullByte.Core.Domain;

namespace NullByte.Core.Services;

/// <summary>Describes the outcome of a cloud-server purchase or upgrade.</summary>
public sealed record CloudServerOperationResult(
    bool Succeeded,
    string FailureMessage,
    double Cost,
    Server? Server)
{
    public static CloudServerOperationResult Failed(string message)
        => new(false, message, 0, null);
}
