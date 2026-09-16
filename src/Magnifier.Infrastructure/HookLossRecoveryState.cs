namespace Magnifier.Infrastructure;

/// <summary>
/// Keeps a confirmed dead hook from abandoning a relayed press while its physical
/// release can no longer arrive through the dead callback.
/// </summary>
internal sealed class HookLossRecoveryState
{
    internal enum Action { None, WaitForPhysicalRelease, Reinstall, StopAfterPhysicalRelease }

    private bool _pending;
    private bool _relayOwnedAtLoss;

    public Action Decide(HookLivenessMonitor.Action livenessAction, bool relayOwnsInput, int osButtons)
    {
        if (_pending)
        {
            if (osButtons != 0) return Action.WaitForPhysicalRelease;
            var mustStop = _relayOwnedAtLoss || relayOwnsInput;
            Reset();
            return mustStop ? Action.StopAfterPhysicalRelease : Action.Reinstall;
        }

        if (livenessAction != HookLivenessMonitor.Action.Reinstall) return Action.None;
        if (osButtons == 0) return relayOwnsInput ? Action.StopAfterPhysicalRelease : Action.Reinstall;

        _pending = true;
        _relayOwnedAtLoss = relayOwnsInput;
        return Action.WaitForPhysicalRelease;
    }

    public void Reset()
    {
        _pending = false;
        _relayOwnedAtLoss = false;
    }
}
