using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Action = Magnifier.Infrastructure.HookLossRecoveryState.Action;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class HookLossRecoveryStateTests
{
    [TestMethod]
    public void ConfirmedLossDuringRelayedDrag_WaitsForPhysicalReleaseThenStops()
    {
        var recovery = new HookLossRecoveryState();

        Assert.AreEqual(Action.WaitForPhysicalRelease,
            recovery.Decide(HookLivenessMonitor.Action.Reinstall, relayOwnsInput: true, osButtons: 1));
        Assert.AreEqual(Action.WaitForPhysicalRelease,
            recovery.Decide(HookLivenessMonitor.Action.None, relayOwnsInput: true, osButtons: 1));
        Assert.AreEqual(Action.StopAfterPhysicalRelease,
            recovery.Decide(HookLivenessMonitor.Action.None, relayOwnsInput: true, osButtons: 0));
    }

    [TestMethod]
    public void ConfirmedLossForUnownedPress_WaitsThenReinstallsAfterRelease()
    {
        var recovery = new HookLossRecoveryState();

        Assert.AreEqual(Action.WaitForPhysicalRelease,
            recovery.Decide(HookLivenessMonitor.Action.Reinstall, relayOwnsInput: false, osButtons: 1));
        Assert.AreEqual(Action.Reinstall,
            recovery.Decide(HookLivenessMonitor.Action.None, relayOwnsInput: false, osButtons: 0));
    }

    [TestMethod]
    public void ReleasedRelayedPress_ProducesExactlyOneSyntheticUpWhenStopped()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));

        var recovery = new HookLossRecoveryState();
        Assert.AreEqual(Action.WaitForPhysicalRelease,
            recovery.Decide(HookLivenessMonitor.Action.Reinstall, relayOwnsInput: true, osButtons: 1));

        state.ObservePhysicalButton(false);
        Assert.AreEqual(Action.StopAfterPhysicalRelease,
            recovery.Decide(HookLivenessMonitor.Action.None, relayOwnsInput: true, osButtons: 0));
        state.Stop();

        Assert.IsFalse(state.IsRequested);
        Assert.IsFalse(state.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "up" }, input.Calls);
    }

    private sealed class RecordingInput : IPointerInput
    {
        public List<string> Calls { get; } = [];
        public void MoveTo(ScreenPoint point) => Calls.Add($"move:{point.X},{point.Y}");
        public void LeftButtonDown() => Calls.Add("down");
        public void LeftButtonUp() => Calls.Add("up");
    }
}
