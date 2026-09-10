using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class LensInputStateTests
{
    [TestMethod]
    public void DefaultAndReentry_RequireExplicitArmWithoutHeldPhysicalButton()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);

        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.Begin(new ScreenPoint(1, 2)));
        Assert.IsFalse(state.Arm(true));
        Assert.IsTrue(state.IsWaitingForRelease);
        Assert.IsFalse(state.Arm(false));
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.IsEnabled);
        Assert.IsTrue(state.Arm(false));
        state.Stop();
        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.Begin(new ScreenPoint(1, 2)));
        Assert.AreEqual(0, input.Calls.Count);
    }

    [TestMethod]
    public void CurveAndReturnDrag_PreservesEveryMoveUntilOneRelease()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new ScreenPoint(-20, 10));
        state.Move(new ScreenPoint(-15, 20));
        state.Move(new ScreenPoint(-10, 25));
        state.Move(new ScreenPoint(-15, 20));
        state.ObservePhysicalButton(false);
        state.Complete(new ScreenPoint(-20, 10));

        CollectionAssert.AreEqual(new[] { "move:-20,10", "down", "move:-15,20", "move:-10,25", "move:-15,20", "move:-20,10", "up" }, input.Calls);
        Assert.IsTrue(state.IsEnabled);
        Assert.IsFalse(state.IsPressed);
    }

    [TestMethod]
    public void BoundaryStop_ReleasesAtLastTargetAndRequiresPhysicalReleaseBeforeRearm()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new ScreenPoint(10, 20));
        state.Move(new ScreenPoint(11, 21));

        state.Stop();
        state.Move(new ScreenPoint(900, 900));
        Assert.IsFalse(state.Arm(true));
        Assert.IsFalse(state.Begin(new ScreenPoint(900, 900)));
        Assert.IsFalse(state.Arm(false));
        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.IsPressed);
        Assert.AreEqual(new ScreenPoint(11, 21), state.LastTarget);
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.IsEnabled);
        Assert.IsTrue(state.Arm(false));
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "move:11,21", "up" }, input.Calls);
    }

    [TestMethod]
    public void MoveFailure_DisablesAndWaitsForReleaseWithoutAutoRearm()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new ScreenPoint(10, 20));
        input.ThrowOnMove = true;

        Assert.ThrowsException<InvalidOperationException>(() => state.Move(new ScreenPoint(11, 21)));

        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.IsPressed);
        Assert.IsTrue(state.IsWaitingForRelease);
        Assert.AreEqual(new ScreenPoint(10, 20), state.LastTarget);
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.IsEnabled);
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "move:11,21", "up" }, input.Calls);
    }

    [TestMethod]
    public void StopFailedUp_RetainsPendingReleaseAndRetriesWithoutMove()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new ScreenPoint(10, 20));
        input.ThrowOnUp = true;

        Assert.ThrowsException<InvalidOperationException>(state.Stop);
        Assert.IsTrue(state.IsPressed);
        Assert.IsFalse(state.IsEnabled);
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.Arm(false));
        state.Move(new ScreenPoint(900, 900));
        input.ThrowOnUp = false;
        state.Stop();

        Assert.IsFalse(state.IsPressed);
        Assert.IsFalse(state.IsEnabled);
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "up", "up" }, input.Calls);
    }

    [TestMethod]
    public void CompleteFailedUp_LeavesPendingReleaseUntilExplicitStop()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new ScreenPoint(10, 20));
        state.ObservePhysicalButton(false);
        input.ThrowOnUp = true;

        Assert.ThrowsException<InvalidOperationException>(() => state.Complete(new ScreenPoint(11, 21)));
        Assert.IsTrue(state.IsPressed);
        Assert.IsFalse(state.IsEnabled);
        Assert.AreEqual(1, input.Calls.Count(call => call == "up"));
        input.ThrowOnUp = false;
        state.Stop();

        Assert.IsFalse(state.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "move:11,21", "up", "up" }, input.Calls);
    }

    [TestMethod]
    public void RepeatedBeginFailure_StopsActiveDragAndRequiresPhysicalRelease()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new ScreenPoint(10, 20));

        Assert.ThrowsException<InvalidOperationException>(() => state.Begin(new ScreenPoint(11, 21)));

        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.IsPressed);
        Assert.IsTrue(state.IsWaitingForRelease);
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "up" }, input.Calls);
    }

    [TestMethod]
    public void WindowHandlePress_GeometryStopDoesNotEnterRelayDrain()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true); // Native WPF thumb owns this press.

        state.Stop(ownsPhysicalPress: false);
        state.Stop(ownsPhysicalPress: false); // Repeated geometry updates during drag.

        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.IsPressed);
        Assert.IsFalse(state.IsWaitingForRelease);
        Assert.AreEqual(0, input.Calls.Count);
        Assert.IsFalse(state.Arm(true)); // Input still cannot rearm with a held button.
        state.ObservePhysicalButton(false);
        Assert.IsTrue(state.Arm(false));
    }

    [TestMethod]
    public void RealTargetPress_CannotSkipReleaseDrainWithWindowAdjustmentFlag()
    {
        var input = new RecordingPointerInput();
        var state = new LensInputState(input);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new ScreenPoint(42, 84));

        state.Stop(ownsPhysicalPress: false);

        Assert.IsFalse(state.IsEnabled);
        Assert.IsFalse(state.IsPressed);
        Assert.IsTrue(state.IsWaitingForRelease);
        Assert.IsFalse(state.Arm(false));
        CollectionAssert.AreEqual(new[] { "move:42,84", "down", "up" }, input.Calls);
    }

    private sealed class RecordingPointerInput : IPointerInput
    {
        public List<string> Calls { get; } = [];
        public bool ThrowOnMove { get; set; }
        public bool ThrowOnUp { get; set; }

        public void MoveTo(ScreenPoint point)
        {
            Calls.Add($"move:{point.X},{point.Y}");
            if (ThrowOnMove)
            {
                throw new InvalidOperationException("move failed");
            }
        }

        public void LeftButtonDown() => Calls.Add("down");

        public void LeftButtonUp()
        {
            Calls.Add("up");
            if (ThrowOnUp)
            {
                throw new InvalidOperationException("up failed");
            }
        }
    }
}
