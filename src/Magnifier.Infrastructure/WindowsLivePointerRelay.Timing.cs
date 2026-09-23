using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    // Worker-owned. Only the latest edit waits; an older pending revision is superseded.
    private (PointerTimingSettings Timing, long Revision)? _pendingTiming;
    private long _appliedTimingRevision;
    private PointerTimingStatus _timingStatus = new(PointerTimingSettings.Default, 0, null, 0);

    public event Action<PointerTimingStatus>? TimingChanged;
    public PointerTimingStatus TimingStatus => Volatile.Read(ref _timingStatus);

    public Task ApplyTimingAsync(PointerTimingSettings timing, long revision)
    {
        if (!timing.IsValid()) return Task.FromException(new ArgumentOutOfRangeException(nameof(timing)));
        return Dispatch(() =>
        {
            _pendingTiming = (timing, revision);
            if (!TryApplyPendingTiming()) PublishTiming();
        });
    }

    // Called at idle boundaries (sequence drained, stop/cancel). Never interrupts a gesture.
    private bool TryApplyPendingTiming()
    {
        if (_pendingTiming is not { } pending || !_sequence.TryApplyTiming(pending.Timing)) return false;
        _pendingTiming = null;
        _appliedTimingRevision = pending.Revision;
        RecordRelayPath("timing-applied", $"입력 타이밍 rev{pending.Revision} {pending.Timing}");
        PublishTiming();
        return true;
    }

    private void PublishTiming()
    {
        var status = new PointerTimingStatus(_sequence.Timing, _appliedTimingRevision,
            _pendingTiming?.Timing, _pendingTiming?.Revision ?? 0);
        Volatile.Write(ref _timingStatus, status);
        TimingChanged?.Invoke(status);
    }

    private string TimingLabel => $"rev{_appliedTimingRevision} {_sequence.Timing}";
}
