namespace Magnifier.Core;

/// <summary>재개 경계 뒤에 도착한 새 화면 프레임만 입력 재무장에 쓴다.</summary>
public sealed class FrameFreshnessGate
{
    private long _generation;
    private long _minimumGeneration = 1;
    private long _timestamp = long.MinValue;

    public void RecordFrame(long timestamp)
    {
        Interlocked.Exchange(ref _timestamp, timestamp);
        Interlocked.Increment(ref _generation);
    }

    /// <summary>이 호출 뒤 기록된 다음 프레임이 올 때까지 입력을 막는다.</summary>
    public void RequireNextFrame()
    {
        Interlocked.Exchange(ref _minimumGeneration, Interlocked.Read(ref _generation) + 1);
        Interlocked.Exchange(ref _timestamp, long.MinValue);
    }

    public bool IsFresh(long now, long maximumAgeMilliseconds)
    {
        if (maximumAgeMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(maximumAgeMilliseconds));
        var timestamp = Interlocked.Read(ref _timestamp);
        if (timestamp == long.MinValue || Interlocked.Read(ref _generation) < Interlocked.Read(ref _minimumGeneration))
        {
            return false;
        }

        var age = now - timestamp;
        return age >= 0 && age < maximumAgeMilliseconds;
    }
}
