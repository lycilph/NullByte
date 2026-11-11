namespace NullByte.Core.Scripting;

/// <summary>
/// The entry point of a compiled player script. Players never write this
/// themselves: the script compiler wraps their top-level statements in a
/// generated class that implements it.
/// </summary>
public interface IScript
{
    Task Run(INullByteApi nb, string[] args);
}