using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class PointerInputSessionTests
{
    [TestMethod]
    public void Begin_DoesNotSendInput_WhenGateIsDisabled()
    {
        var pointerInput = new RecordingPointerInput();
        var session = new PointerInputSession(pointerInput);

        var began = session.Begin(new ScreenPoint(100, 200));

        Assert.IsFalse(began);
        Assert.IsFalse(session.IsPressed);
        CollectionAssert.AreEqual(Array.Empty<string>(), pointerInput.Calls);
    }

    [TestMethod]
    public void Complete_SendsOneDownMoveUpSequence_WhenGateIsEnabled()
    {
        var pointerInput = new RecordingPointerInput();
        var session = new PointerInputSession(pointerInput);
        session.SetInputEnabled(true);

        session.Begin(new ScreenPoint(100, 200));
        session.Move(new ScreenPoint(110, 210));
        session.Complete(new ScreenPoint(120, 220));

        CollectionAssert.AreEqual(
            new[] { "move:100,200", "down", "move:110,210", "move:120,220", "up" },
            pointerInput.Calls);
        Assert.IsFalse(session.IsPressed);
    }

    [TestMethod]
    public void Disable_CancelsPressedSessionWithOneRelease()
    {
        var pointerInput = new RecordingPointerInput();
        var session = new PointerInputSession(pointerInput);
        session.SetInputEnabled(true);
        session.Begin(new ScreenPoint(100, 200));

        session.SetInputEnabled(false);
        session.Cancel();

        CollectionAssert.AreEqual(new[] { "move:100,200", "down", "up" }, pointerInput.Calls);
        Assert.IsFalse(session.IsInputEnabled);
        Assert.IsFalse(session.IsPressed);
    }

    [TestMethod]
    public void MoveFailure_AttemptsReleaseBeforeRethrowing()
    {
        var pointerInput = new RecordingPointerInput();
        var session = new PointerInputSession(pointerInput);
        session.SetInputEnabled(true);
        session.Begin(new ScreenPoint(100, 200));
        pointerInput.ThrowOnMove = true;

        Assert.ThrowsException<InvalidOperationException>(() => session.Move(new ScreenPoint(110, 210)));

        CollectionAssert.AreEqual(new[] { "move:100,200", "down", "move:110,210", "up" }, pointerInput.Calls);
        Assert.IsFalse(session.IsPressed);
        Assert.IsFalse(session.IsInputEnabled);
        Assert.AreEqual(new ScreenPoint(100, 200), session.LastPoint);
    }

    [TestMethod]
    public void BeginMoveFailure_DisablesGateWithoutCreatingPressedState()
    {
        var input = new RecordingPointerInput { ThrowOnMove = true };
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);

        Assert.ThrowsException<InvalidOperationException>(() => session.Begin(new ScreenPoint(4, 5)));

        Assert.IsFalse(session.IsInputEnabled);
        Assert.IsFalse(session.IsPressed);
        Assert.IsNull(session.LastPoint);
        CollectionAssert.AreEqual(new[] { "move:4,5" }, input.Calls);
    }

    [TestMethod]
    public void BeginDownFailure_DisablesGateAndAttemptsRelease()
    {
        var input = new RecordingPointerInput { ThrowOnDown = true };
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);

        Assert.ThrowsException<InvalidOperationException>(() => session.Begin(new ScreenPoint(4, 5)));

        Assert.IsFalse(session.IsInputEnabled);
        Assert.IsFalse(session.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:4,5", "down", "up" }, input.Calls);
    }

    [TestMethod]
    public void CompleteMoveFailure_DisablesGateAndReleasesWithoutUpdatingLastPoint()
    {
        var input = new RecordingPointerInput();
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);
        session.Begin(new ScreenPoint(-5, 10));
        input.ThrowOnMove = true;

        Assert.ThrowsException<InvalidOperationException>(() => session.Complete(new ScreenPoint(100, 200)));

        Assert.IsFalse(session.IsInputEnabled);
        Assert.IsFalse(session.IsPressed);
        Assert.AreEqual(new ScreenPoint(-5, 10), session.LastPoint);
        CollectionAssert.AreEqual(new[] { "move:-5,10", "down", "move:100,200", "up" }, input.Calls);
    }

    [TestMethod]
    public void FailedUp_RetainsPressedStateAndDisablesGateUntilCancelSucceeds()
    {
        var input = new RecordingPointerInput();
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);
        session.Begin(new ScreenPoint(20, 30));
        input.ThrowOnUp = true;

        Assert.ThrowsException<InvalidOperationException>(() => session.Complete(new ScreenPoint(21, 31)));
        Assert.IsTrue(session.IsPressed);
        Assert.IsFalse(session.IsInputEnabled);
        Assert.ThrowsException<InvalidOperationException>(() => session.SetInputEnabled(true));
        session.Move(new ScreenPoint(999, 999));
        input.ThrowOnUp = false;
        session.Cancel();

        Assert.IsFalse(session.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:20,30", "down", "move:21,31", "up", "up" }, input.Calls);
    }

    [TestMethod]
    public void MoveAndReleaseFailure_RetainsPendingReleaseForDisableRetry()
    {
        var input = new RecordingPointerInput();
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);
        session.Begin(new ScreenPoint(20, 30));
        input.ThrowOnMove = true;
        input.ThrowOnUp = true;

        var error = Assert.ThrowsException<InvalidOperationException>(() => session.Move(new ScreenPoint(21, 31)));

        Assert.AreEqual("move failed", error.Message);
        Assert.IsTrue(session.IsPressed);
        Assert.IsFalse(session.IsInputEnabled);
        input.ThrowOnUp = false;
        session.SetInputEnabled(false);
        Assert.IsFalse(session.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:20,30", "down", "move:21,31", "up", "up" }, input.Calls);
    }

    private sealed class RecordingPointerInput : IPointerInput
    {
        public List<string> Calls { get; } = [];

        public bool ThrowOnMove { get; set; }

        public bool ThrowOnDown { get; set; }

        public bool ThrowOnUp { get; set; }

        public void MoveTo(ScreenPoint point)
        {
            Calls.Add($"move:{point.X},{point.Y}");
            if (ThrowOnMove)
            {
                throw new InvalidOperationException("move failed");
            }
        }

        public void LeftButtonDown()
        {
            Calls.Add("down");
            if (ThrowOnDown)
            {
                throw new InvalidOperationException("down failed");
            }
        }

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
