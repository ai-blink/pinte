using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class ContinuousInputTests
{
    [TestMethod]
    public void OpenRequest_WaitsForHeldEntryClickWithoutLockingWindowHandles()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        Assert.IsFalse(state.TryResume(false));
        state.RequestStart();
        state.ObservePhysicalButton(true);
        Assert.IsFalse(state.TryResume(true));
        Assert.IsFalse(state.IsWaitingForRelease);
        state.ObservePhysicalButton(false);
        Assert.IsTrue(state.TryResume(false));
        Assert.AreEqual(0, input.Calls.Count);
    }

    [TestMethod]
    public void WindowAdjustment_PreservesRequestWithoutStealingHeldThumb()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Pause(ownsPhysicalPress: false);
        state.Pause(ownsPhysicalPress: false);
        Assert.IsTrue(state.IsRequested);
        Assert.IsFalse(state.IsWaitingForRelease);
        Assert.IsFalse(state.TryResume(false)); // Stale sampling cannot skip observed button state.
        state.ObservePhysicalButton(false);
        Assert.IsTrue(state.TryResume(false));
        Assert.AreEqual(0, input.Calls.Count);
    }

    [TestMethod]
    public void BoundaryPause_ReleasesOnceThenResumesWithoutSynthesizingAnotherClick()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        state.Move(new(15, 25));
        state.Pause();
        Assert.IsTrue(state.IsRequested);
        Assert.IsTrue(state.IsWaitingForRelease);
        Assert.IsFalse(state.TryResume(false));
        state.Move(new(900, 900));
        state.ObservePhysicalButton(false);
        Assert.IsTrue(state.TryResume(false));
        Assert.IsFalse(state.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "move:15,25", "up" }, input.Calls);
    }

    [TestMethod]
    public void ExplicitStop_CancelsPendingResumeUntilNewOpenOrResumeRequest()
    {
        var state = new LensInputState(new RecordingInput());
        state.Arm(false);
        state.Pause();
        state.Stop();
        state.Pause(ownsPhysicalPress: false); // Later layout changes cannot undo explicit Stop.
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.IsRequested);
        Assert.IsFalse(state.TryResume(false));
        state.RequestStart();
        Assert.IsTrue(state.TryResume(false));
    }

    [TestMethod]
    public void FailedPauseRelease_CancelsResumeAndKeepsReleasePending()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        input.FailUp = true;
        Assert.ThrowsException<InvalidOperationException>(() => state.Pause());
        Assert.IsTrue(state.IsPressed);
        Assert.IsFalse(state.IsRequested);
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.TryResume(false));
        input.FailUp = false;
        state.Stop();
        Assert.IsFalse(state.IsPressed);
        Assert.IsFalse(state.TryResume(false));
    }

    [TestMethod]
    public void MoveFailure_DoesNotResumeAfterPhysicalRelease()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        input.FailMove = true;
        Assert.ThrowsException<InvalidOperationException>(() => state.Move(new(15, 25)));
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.IsRequested);
        Assert.IsFalse(state.TryResume(false));
    }

    private sealed class RecordingInput : IPointerInput
    {
        public List<string> Calls { get; } = [];
        public bool FailUp { get; set; }
        public bool FailMove { get; set; }
        public void MoveTo(ScreenPoint point)
        {
            Calls.Add($"move:{point.X},{point.Y}");
            if (FailMove) throw new InvalidOperationException("move failed");
        }
        public void LeftButtonDown() => Calls.Add("down");
        public void LeftButtonUp()
        {
            Calls.Add("up");
            if (FailUp) throw new InvalidOperationException("up failed");
        }
    }
}
