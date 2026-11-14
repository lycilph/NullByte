namespace NullByte.Core.Scripting;

/// <summary>
/// Declares the static RAM cost of an NB API method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RamCostAttribute(double cost) : Attribute
{
    public double Cost { get; } = cost;
}