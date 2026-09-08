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
    }

    private sealed class RecordingPointerInput : IPointerInput
    {
        public List<string> Calls { get; } = [];

        public bool ThrowOnMove { get; set; }

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
        }

        public void LeftButtonUp()
        {
            Calls.Add("up");
        }
    }
}
