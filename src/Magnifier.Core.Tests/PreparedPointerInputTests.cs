using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class PreparedPointerInputTests
{
    [TestMethod]
    public void PrepareThenPress_DoesNotRepeatMovementOrPressEarly()
    {
        var input = new Input();
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);
        Assert.IsTrue(session.PrepareBegin(new(10, 20)));
        Assert.IsFalse(session.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:10,20" }, input.Calls);
        Assert.IsTrue(session.BeginPrepared());
        CollectionAssert.AreEqual(new[] { "move:10,20", "down" }, input.Calls);
        Assert.IsTrue(session.IsPressed);
    }

    [TestMethod]
    public void CancelOrDisable_InvalidatesPreparedPressEvenAfterRearming()
    {
        var input = new Input();
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);
        session.PrepareBegin(new(10, 20));
        session.Cancel();
        Assert.ThrowsException<InvalidOperationException>(() => session.BeginPrepared());
        session.PrepareBegin(new(30, 40));
        session.SetInputEnabled(false);
        session.SetInputEnabled(true);
        Assert.ThrowsException<InvalidOperationException>(() => session.BeginPrepared());
        CollectionAssert.AreEqual(new[] { "move:10,20", "move:30,40" }, input.Calls);
    }

    [TestMethod]
    public void DisabledPrepare_DoesNotMoveOrPress()
    {
        var input = new Input();
        var session = new PointerInputSession(input);
        Assert.IsFalse(session.PrepareBegin(new(10, 20)));
        Assert.IsFalse(session.BeginPrepared());
        Assert.AreEqual(0, input.Calls.Count);
    }

    [TestMethod]
    public void PreparedDownFailure_RetainsReleaseResponsibilityUntilRetrySucceeds()
    {
        var input = new Input { FailDown = true, FailUp = true };
        var session = new PointerInputSession(input);
        session.SetInputEnabled(true);
        session.PrepareBegin(new(10, 20));
        Assert.ThrowsException<InvalidOperationException>(() => session.BeginPrepared());
        Assert.IsTrue(session.IsPressed);
        Assert.IsFalse(session.IsInputEnabled);
        input.FailUp = false;
        session.Cancel();
        Assert.IsFalse(session.IsPressed);
        CollectionAssert.AreEqual(new[] { "move:10,20", "down", "up", "up" }, input.Calls);
    }

    [TestMethod]
    public void LensPause_InvalidatesPreparationAndPreservesRequestedResume()
    {
        var input = new Input();
        var state = new LensInputState(input);
        state.Arm(false);
        state.PrepareBegin(new(10, 20));
        state.Pause(ownsPhysicalPress: false);
        Assert.IsTrue(state.TryResume(false));
        Assert.ThrowsException<InvalidOperationException>(() => state.BeginPrepared());
        Assert.IsFalse(state.IsRequested);
        CollectionAssert.AreEqual(new[] { "move:10,20" }, input.Calls);
    }

    private sealed class Input : IPointerInput
    {
        public List<string> Calls { get; } = [];
        public bool FailDown, FailUp;
        public void MoveTo(ScreenPoint point) => Calls.Add($"move:{point.X},{point.Y}");
        public void LeftButtonDown()
        {
            Calls.Add("down");
            if (FailDown) throw new InvalidOperationException("Down failed");
        }
        public void LeftButtonUp()
        {
            Calls.Add("up");
            if (FailUp) throw new InvalidOperationException("Up failed");
        }
    }
}
