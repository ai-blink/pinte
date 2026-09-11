namespace Magnifier.Infrastructure;

internal readonly record struct CaptureSample(bool Succeeded, nint Capture, nint Root,
    uint CaptureThread, bool TargetAvailable, bool FocusMovedAway);
internal readonly record struct CaptureTrace(uint Thread, long TargetRoot, long Previous,
    long Current, long CurrentRoot, bool ReadSucceeded, uint CaptureThread,
    bool TargetAvailable, bool FocusMovedAway);

// Capture is optional in target apps. Track only capture belonging to this press's source window.
// This never selects the input destination; SendInput still uses the mapped screen position.
internal sealed class PointerCaptureMonitor
{
    private uint _thread;
    private nint _root, _observed;
    public CaptureTrace Trace { get; private set; }

    public void Begin(uint thread, nint root)
    {
        _thread = thread;
        _root = root;
        _observed = 0;
        Trace = new(thread, (long)root, 0, 0, 0, false, 0, false, false);
    }

    public bool LostCapture(Func<uint, nint, CaptureSample> read, Func<bool> releasePending)
    {
        if (_thread == 0 || _root == 0) return false;
        var sample = read(_thread, _root); // Capture always comes from the source thread.
        Trace = new(_thread, (long)_root, (long)_observed, (long)sample.Capture,
            (long)sample.Root, sample.Succeeded, sample.CaptureThread,
            sample.TargetAvailable, sample.FocusMovedAway);
        if (releasePending()) return false;
        if (!sample.Succeeded || !sample.TargetAvailable) return true;
        // Drag detection can release capture while the button is still down. Absence
        // alone is not cancellation; require the foreground to have left the source too.
        if (sample.Capture == 0) return _observed != 0 && sample.FocusMovedAway;
        // A helper window on the source GUI thread can have a different root (e.g.
        // a drag loop). Window identity changes must not manufacture a button Up.
        if (sample.CaptureThread != _thread) return _observed != 0;
        _observed = sample.Capture;
        return false;
    }

    public void Reset() => Begin(0, 0);
}
