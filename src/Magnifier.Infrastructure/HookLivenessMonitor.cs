namespace Magnifier.Infrastructure;

// Windows removes a WH_MOUSE_LL hook whose callback exceeds LowLevelHooksTimeout and
// gives the process no signal (documented for Windows 7 and later). Afterwards the relay
// still holds a valid request and fresh frames, but no click ever reaches it.
//
// A silent hook alone is weak evidence: on a machine whose cursor is driven entirely by
// injected moves (assistive devices), any brief lull looks the same as a dead hook and a
// naive reinstall storms — tearing down live drags. So suspicion never reinstalls directly.
// It asks the relay to inject one tagged probe, and only a probe that never echoes back
// through the hook confirms death. A live hook echoes the probe and clears the suspicion.
internal sealed class HookLivenessMonitor
{
    public enum Action { None, Probe, Reinstall }

    private readonly int _requiredStrikes;
    private readonly int _probeGraceTicks;
    private readonly long _reinstallCooldownMilliseconds;
    private long _hookActivity, _sampledActivity, _activityAtProbe;
    private long? _lastReinstall;
    private int _lastX, _lastY, _lastButtons;
    private bool _hasSample, _probing;
    private int _ticksSinceProbe;

    public HookLivenessMonitor(int requiredStrikes = 3, int probeGraceTicks = 4, long reinstallCooldownMilliseconds = 1000)
    {
        if (requiredStrikes < 1) throw new ArgumentOutOfRangeException(nameof(requiredStrikes));
        if (probeGraceTicks < 1) throw new ArgumentOutOfRangeException(nameof(probeGraceTicks));
        _requiredStrikes = requiredStrikes;
        _probeGraceTicks = probeGraceTicks;
        _reinstallCooldownMilliseconds = reinstallCooldownMilliseconds;
    }

    /// <summary>Consecutive samples where the OS pointer changed while the hook stayed silent.</summary>
    public int Strikes { get; private set; }

    public bool Verifying => _probing;

    /// <summary>Call from the hook callback for every message, before any filtering.</summary>
    public void NoteHookActivity() => _hookActivity++;

    /// <summary>Call from the relay timer. Probe = inject one tagged self-test; Reinstall = hook confirmed dead.</summary>
    public Action Sample(long now, int cursorX, int cursorY, int buttons)
    {
        var hookSeen = _hookActivity != _sampledActivity;
        _sampledActivity = _hookActivity;
        var pointerChanged = _hasSample && (cursorX != _lastX || cursorY != _lastY || buttons != _lastButtons);
        _lastX = cursorX;
        _lastY = cursorY;
        _lastButtons = buttons;
        _hasSample = true;

        if (_probing)
        {
            // A live hook echoes the tagged probe (or any other event) as activity.
            if (_hookActivity != _activityAtProbe) { _probing = false; Strikes = 0; return Action.None; }
            if (++_ticksSinceProbe < _probeGraceTicks) return Action.None;
            _probing = false;
            Strikes = 0;
            _lastReinstall = now;
            return Action.Reinstall;
        }

        if (hookSeen)
        {
            Strikes = 0;
            return Action.None;
        }
        // An idle pointer is no evidence either way. Only a silent hook under real input counts.
        if (!pointerChanged) return Action.None;
        Strikes = Math.Min(Strikes + 1, _requiredStrikes);
        if (Strikes < _requiredStrikes) return Action.None;
        if (_lastReinstall is not null && now - _lastReinstall.Value < _reinstallCooldownMilliseconds) return Action.None;
        _probing = true;
        _ticksSinceProbe = 0;
        _activityAtProbe = _hookActivity;
        return Action.Probe;
    }

    public void NoteReinstalled(long now)
    {
        _lastReinstall = now;
        Strikes = 0;
        _probing = false;
        _sampledActivity = _hookActivity;
    }
}
