using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class RelayCommandPumpTests
{
    [TestMethod]
    public void ObservedUp_CompletesBeforeTimerCanCancelRequestForReleasedCapture()
    {
        var input = new RecordingInput();
        var state = Press(input);
        var pump = new RelayCommandPump();
        pump.Enqueue(() =>
        {
            state.ObservePhysicalButton(false);
            state.Complete(new(15, 25));
            state.Pause();
        }, leftRelease: true);

        // Timer already returned by the native message pump; a hook queued Up before dispatch.
        pump.ProcessMessage(timer: true, () => { if (state.IsPressed) state.Stop(); });

        Assert.IsTrue(state.IsRequested, "정상 Up을 명시적 중지로 처리하면 안 됩니다.");
        Assert.IsFalse(state.IsPressed);
        Assert.IsFalse(pump.HasPendingLeftRelease);
        Assert.IsTrue(state.TryResume(false));
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "move:15,25", "up" }, input.Calls);
    }

    [TestMethod]
    public void StillHeldCaptureLoss_RemainsHardStopAndReleasesOnce()
    {
        var input = new RecordingInput();
        var state = Press(input);
        var pump = new RelayCommandPump();
        pump.ProcessMessage(timer: true, () => { if (state.IsPressed) state.Stop(); });
        Assert.IsFalse(state.IsRequested);
        Assert.IsFalse(state.IsPressed);
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.TryResume(false));
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "up" }, input.Calls);
    }

    [TestMethod]
    public void UpThenNextDown_DoesNotErasePendingReleaseObservation()
    {
        var pump = new RelayCommandPump();
        var calls = new List<string>();
        pump.Enqueue(() => calls.Add("up"), leftRelease: true);
        pump.Enqueue(() => calls.Add("down"));
        Assert.IsTrue(pump.HasPendingLeftRelease);
        pump.ProcessMessage(timer: false, () => Assert.Fail("Not a timer"));
        Assert.IsFalse(pump.HasPendingLeftRelease);
        CollectionAssert.AreEqual(new[] { "up", "down" }, calls);
    }

    [TestMethod]
    public void UpFailure_DoesNotRestoreRequestOrLosePendingButton()
    {
        var input = new RecordingInput();
        var state = Press(input);
        var pump = new RelayCommandPump();
        input.FailUp = true;
        pump.Enqueue(() =>
        {
            state.ObservePhysicalButton(false);
            try { state.Complete(new(15, 25)); }
            catch (InvalidOperationException) { }
        }, leftRelease: true);
        pump.ProcessMessage(timer: true, () => { });
        Assert.IsFalse(state.IsRequested);
        Assert.IsTrue(state.IsPressed);
        Assert.IsFalse(state.TryResume(false));
        Assert.IsFalse(pump.HasPendingLeftRelease);
        input.FailUp = false;
        state.Stop();
        Assert.IsFalse(state.IsPressed);
    }

    private static LensInputState Press(RecordingInput input)
    {
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        return state;
    }

    private sealed class RecordingInput : IPointerInput
    {
        public List<string> Calls { get; } = [];
        public bool FailUp { get; set; }
        public void MoveTo(ScreenPoint point) => Calls.Add($"move:{point.X},{point.Y}");
        public void LeftButtonDown() => Calls.Add("down");
        public void LeftButtonUp()
        {
            Calls.Add("up");
            if (FailUp) throw new InvalidOperationException("Up failed");
        }
    }
}
