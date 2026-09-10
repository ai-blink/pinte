namespace Magnifier.Infrastructure;

internal readonly record struct CaptureSample(bool Succeeded, nint Capture, nint Root);
internal readonly record struct CaptureTrace(uint Thread, long TargetRoot, long Previous,
    long Current, long CurrentRoot, bool ReadSucceeded);

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
        Trace = new(thread, (long)root, 0, 0, 0, false);
    }

    public bool LostCapture(Func<uint, CaptureSample> read, Func<bool> releasePending)
    {
        if (_thread == 0 || _root == 0) return false;
        var sample = read(_thread); // Never query whichever thread happens to be foreground.
        Trace = new(_thread, (long)_root, (long)_observed, (long)sample.Capture,
            (long)sample.Root, sample.Succeeded);
        if (releasePending()) return false;
        if (!sample.Succeeded) return true; // The source GUI thread closed or became unreadable.
        if (sample.Capture == 0) return _observed != 0;
        if (sample.Root != _root) return _observed != 0;
        // A parent/child handoff inside the same source root continues the same drag.
        _observed = sample.Capture;
        return false;
    }

    public void Reset() => Begin(0, 0);
}
