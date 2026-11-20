using System.Security.Cryptography;
using System.Text;

namespace NullByte.Editor;

public sealed class EditorToken
{
    private EditorToken(string value) => Value = value;

    public string Value { get; }

    public static EditorToken Create() => new(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public bool Matches(string? candidate)
        => candidate is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(Value));
}