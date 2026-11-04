namespace NullByte.Core.Time;

public interface IGameClock
{
    TimeSpan Elapsed { get; }
}