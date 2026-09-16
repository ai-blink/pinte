namespace Magnifier.Infrastructure;

/// <summary>
/// Limits consecutive low-level-hook recoveries without making recovery-free gaps
/// accumulate for the entire process lifetime.
/// </summary>
internal sealed class HookRecoveryLimiter
{
    private readonly int _maximumAttempts;
    private readonly long _quietPeriodMilliseconds;
    private long? _lastAttemptAt;

    public HookRecoveryLimiter(int maximumAttempts = 12, long quietPeriodMilliseconds = 60_000)
    {
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        if (quietPeriodMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(quietPeriodMilliseconds));
        _maximumAttempts = maximumAttempts;
        _quietPeriodMilliseconds = quietPeriodMilliseconds;
    }

    public int Attempts { get; private set; }
    public int MaximumAttempts => _maximumAttempts;

    /// <summary>Reserves one automatic reinstall. A recovery-free interval starts a new budget.</summary>
    public bool TryAcquire(long now)
    {
        if (_lastAttemptAt is not null && now - _lastAttemptAt.Value >= _quietPeriodMilliseconds)
        {
            Attempts = 0;
        }

        _lastAttemptAt = now;
        if (Attempts >= _maximumAttempts) return false;
        Attempts++;
        return true;
    }

    /// <summary>A user-initiated new lens session is a separate recovery cycle.</summary>
    public void Reset()
    {
        Attempts = 0;
        _lastAttemptAt = null;
    }
}
