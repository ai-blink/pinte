using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class TimedPointerSequenceTests
{
    [TestMethod]
    public void QuickTap_SeparatesArrivalHoldRecoveryAndNextInput()
    {
        var h = new Harness();
        h.Begin(new(10, 20));
        h.Now = 10; h.End(new(10, 20));
        h.At(99);
        CollectionAssert.AreEqual(new[] { "0:move:10,20" }, h.Calls);
        h.At(100); h.At(134);
        Assert.IsTrue(h.Sequence.HasPendingRelease);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down" }, h.Calls);
        h.At(135);
        Assert.IsFalse(h.Sequence.HasPendingRelease);
        Assert.IsFalse(h.State.IsPressed);
        h.At(694); Assert.AreEqual(0, h.Restores);
        h.At(695); Assert.AreEqual(1, h.Restores);
        h.At(744); Assert.IsTrue(h.Sequence.IsBusy);
        h.At(745); Assert.IsFalse(h.Sequence.IsBusy);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down", "135:move:10,20", "135:up",
            "695:move:900,800" }, h.Calls);
    }

    [TestMethod]
    public void EarlyDragMoves_AreBufferedAfterDownInOrder()
    {
        var h = new Harness();
        h.Begin(new(10, 20));
        h.Now = 20; h.Move(new(11, 21));
        h.Now = 40; h.Move(new(12, 22));
        h.Now = 50; h.End(new(13, 23));
        CollectionAssert.AreEqual(new[] { "0:move:10,20" }, h.Calls);
        h.At(100); h.At(135);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down", "100:move:11,21",
            "100:move:12,22", "135:move:13,23", "135:up" }, h.Calls);
    }

    [TestMethod]
    public void ThreeFastClicks_KeepTheirOwnCoordinatesAndAttemptIds()
    {
        var h = new Harness();
        h.Begin(new(10, 20), 1);
        h.Now = 10; h.End(new(10, 20));
        h.Now = 20; h.Begin(new(30, 40), 2);
        h.Now = 30; h.End(new(30, 40));
        h.Now = 40; h.Begin(new(50, 60), 3);
        h.Now = 50; h.End(new(50, 60));
        h.ReturnPoint = new(1200, 1300);
        foreach (var t in new long[] { 100, 135, 695, 745, 845, 880, 1440, 1490, 1590, 1625, 2185, 2235 }) h.At(t);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down", "135:move:10,20", "135:up",
            "695:move:1200,1300", "745:move:30,40", "845:down", "880:move:30,40", "880:up",
            "1440:move:1200,1300", "1490:move:50,60", "1590:down", "1625:move:50,60", "1625:up",
            "2185:move:1200,1300" }, h.Calls);
        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, h.PressedAttempts);
        Assert.IsFalse(h.Sequence.IsBusy);
        Assert.IsTrue(h.State.IsRequested);
    }

    [TestMethod]
    public void LateTick_UsesActualDownAndUpTimesNotOldDeadlines()
    {
        var h = new Harness();
        h.Begin(new(10, 20));
        h.End(new(10, 20));
        h.At(180); h.At(214);
        Assert.IsTrue(h.State.IsPressed);
        h.At(215);
        h.At(774); Assert.AreEqual(0, h.Restores);
        h.At(775); Assert.AreEqual(1, h.Restores);
        StringAssert.Contains(h.Calls[1], "180:down");
        StringAssert.Contains(h.Calls[3], "215:up");
    }

    [TestMethod]
    public void SlowNativeDown_DoesNotEatMinimumHoldInterval()
    {
        var h = new Harness { DownCostMs = 80 };
        h.Begin(new(10, 20)); h.End(new(10, 20));
        h.At(100);
        Assert.AreEqual(180, h.Now);
        h.At(214); Assert.IsTrue(h.State.IsPressed);
        h.At(215); Assert.IsFalse(h.State.IsPressed);
    }

    [TestMethod]
    public void ActualCursorDrift_StopsWithoutPressingAtWrongPosition()
    {
        var h = new Harness();
        h.Begin(new(10, 20));
        h.Position = new(500, 600);
        Assert.ThrowsException<InvalidOperationException>(() => h.At(100));
        h.Stop(); h.At(10000);
        CollectionAssert.AreEqual(new[] { "0:move:10,20" }, h.Calls);
        Assert.AreEqual(0, h.Restores);
    }

    [DataTestMethod]
    [DataRow(99)]
    [DataRow(100)]
    [DataRow(101)]
    [DataRow(134)]
    [DataRow(135)]
    [DataRow(136)]
    [DataRow(694)]
    [DataRow(695)]
    [DataRow(696)]
    public void StopAtPhaseBoundaries_DropsAllPendingGestures(int cancelAt)
    {
        var h = new Harness();
        h.Begin(new(10, 20)); h.End(new(10, 20));
        h.Begin(new(30, 40)); h.End(new(30, 40));
        if (cancelAt > 100) h.At(100);
        if (cancelAt > 135) h.At(135);
        h.Now = cancelAt;
        h.Stop();
        var count = h.Calls.Count;
        h.At(10000);
        Assert.AreEqual(count, h.Calls.Count);
        Assert.IsFalse(h.State.IsPressed);
        Assert.IsFalse(h.Sequence.IsBusy);
        Assert.IsFalse(h.State.IsRequested);
        Assert.IsFalse(h.Calls.Any(x => x.Contains("move:30,40")));
    }

    [TestMethod]
    public void RecoveryHover_DoesNotMoveSourceUntilRestore_UsesLatestLensPosition()
    {
        var h = new Harness();
        h.Begin(new(10, 20)); h.End(new(10, 20)); h.At(100); h.At(135);
        var count = h.Calls.Count;
        h.Now = 200; h.Move(new(70, 80)); // No physical gesture is collecting anymore.
        h.ReturnPoint = new(1000, 1100);
        Assert.AreEqual(count, h.Calls.Count);
        h.At(695);
        Assert.AreEqual("695:move:1000,1100", h.Calls.Last());
    }

    [TestMethod]
    public void FailedUp_DoesNotRestoreOrStartNextGesture()
    {
        var h = new Harness();
        h.Begin(new(10, 20)); h.End(new(10, 20));
        h.Begin(new(30, 40)); h.End(new(30, 40));
        h.At(100); h.FailUp = true;
        Assert.ThrowsException<InvalidOperationException>(() => h.At(135));
        Assert.IsTrue(h.State.IsPressed);
        Assert.IsFalse(h.Sequence.IsBusy);
        h.At(10000);
        Assert.AreEqual(0, h.Restores);
        Assert.IsFalse(h.Calls.Any(x => x.Contains("move:30,40")));
        h.FailUp = false; h.Stop();
        Assert.IsFalse(h.State.IsPressed);
    }

    [TestMethod]
    public void FailedDown_ReleasesBeforeDiscardingQueue()
    {
        var h = new Harness();
        h.Begin(new(10, 20)); h.End(new(10, 20));
        h.FailDown = true;
        Assert.ThrowsException<InvalidOperationException>(() => h.At(100));
        Assert.IsFalse(h.State.IsPressed);
        Assert.IsFalse(h.State.IsRequested);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down", "100:up" }, h.Calls);
    }

    [TestMethod]
    public void FailedBufferedMove_ReleasesAndNeverReplaysRemainder()
    {
        var h = new Harness();
        h.Begin(new(10, 20)); h.Move(new(11, 21)); h.End(new(12, 22));
        h.FailMove = true;
        Assert.ThrowsException<InvalidOperationException>(() => h.At(100));
        h.FailMove = false; h.Stop(); h.At(10000);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down", "100:move:11,21", "100:up" }, h.Calls);
    }

    [TestMethod]
    public void BoundedQueue_RejectsOverloadWithoutLosingExistingUpResponsibility()
    {
        var h = new Harness();
        for (var i = 0; i < TimedPointerSequence.MaximumGestures; i++)
        {
            h.Begin(new(i, i)); h.End(new(i, i));
        }
        Assert.ThrowsException<InvalidOperationException>(() => h.Begin(new(100, 100)));
        h.Stop(); h.At(10000);
        Assert.IsFalse(h.Sequence.IsBusy);
        Assert.IsFalse(h.State.IsPressed);
        Assert.AreEqual(1, h.Calls.Count);
    }

    [TestMethod]
    public void PendingMinimumHoldRelease_DoesNotBecomeCaptureLoss()
    {
        var h = new Harness();
        h.Begin(new(10, 20)); h.End(new(10, 20)); h.At(100);
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(11, 22);
        var lost = monitor.LostCapture((_, _) => new(false, 0, 0, 0, false, true),
            () => h.Sequence.HasPendingRelease);
        Assert.IsFalse(lost);
        Assert.IsTrue(h.State.IsPressed);
        h.At(135);
        Assert.IsFalse(h.State.IsPressed);
    }

    [TestMethod]
    public void BufferedPathSpanningTicks_KeepsAllMovesBeforeUp()
    {
        var h = new Harness();
        h.Begin(new(10, 20));
        for (var i = 1; i <= 12; i++) h.Move(new(10 + i, 20 + i));
        h.End(new(22, 32));
        h.At(100);
        Assert.AreEqual(10, h.Calls.Count); // prepare, Down, eight buffered moves
        Assert.IsTrue(h.State.IsPressed);
        h.At(135);
        var expected = new List<string> { "0:move:10,20", "100:down" };
        expected.AddRange(Enumerable.Range(1, 12).Select(i => $"{(i <= 8 ? 100 : 135)}:move:{10 + i},{20 + i}"));
        expected.AddRange(["135:move:22,32", "135:up"]);
        CollectionAssert.AreEqual(expected, h.Calls);
        Assert.AreEqual("135:up", h.Calls.Last());
        Assert.IsFalse(h.State.IsPressed);
    }

    [TestMethod]
    public void PrepareFailure_DoesNotEmitDownOrRestore()
    {
        var h = new Harness { FailMove = true };
        Assert.ThrowsException<InvalidOperationException>(() => h.Begin(new(10, 20)));
        h.FailMove = false; h.Stop(); h.At(10000);
        CollectionAssert.AreEqual(new[] { "0:move:10,20" }, h.Calls);
        Assert.IsFalse(h.Sequence.IsBusy);
    }

    [TestMethod]
    public void RestoreFailure_DiscardsLaterGesturesWithoutNewDown()
    {
        var h = new Harness();
        h.Begin(new(10, 20)); h.End(new(10, 20));
        h.Begin(new(30, 40)); h.End(new(30, 40));
        h.At(100); h.At(135); h.FailMove = true;
        Assert.ThrowsException<InvalidOperationException>(() => h.At(695));
        h.FailMove = false; h.Stop(); h.At(10000);
        Assert.AreEqual(1, h.Calls.Count(x => x.EndsWith(":down")));
        Assert.IsFalse(h.State.IsPressed);
        Assert.IsFalse(h.Sequence.IsBusy);
    }

    internal sealed class Harness : IPointerInput
    {
        public long Now;
        public int DownCostMs, Restores;
        public bool FailDown, FailUp, FailMove;
        public ScreenPoint Position;
        public ScreenPoint ReturnPoint = new(900, 800);
        public List<string> Calls { get; } = [];
        public List<long> PressedAttempts { get; } = [];
        public LensInputState State { get; }
        public TimedPointerSequence Sequence { get; }
        public Harness(PointerTiming? timing = null)
        {
            State = new(this);
            State.Arm(false);
            Sequence = new(State, () => Now, point => point == Position,
                () => { MoveTo(ReturnPoint); Restores++; },
                (stage, _, attempt) => { if (stage == TimedPointerSequence.Trace.Pressed) PressedAttempts.Add(attempt); },
                // Keep the confirmed baseline as an explicit regression fixture.
                // TimedPointerLatencyTests exercises the new production defaults.
                timing ?? new(100, 35, 560, 50));
        }
        public void At(long time) { Now = time; Sequence.Tick(); }
        public void Begin(ScreenPoint point, long attempt = 1)
        { State.ObservePhysicalButton(true); Sequence.Begin(point, attempt); Sequence.Tick(); }
        public void Move(ScreenPoint point) { Sequence.Move(point); Sequence.Tick(); }
        public void End(ScreenPoint point)
        { State.ObservePhysicalButton(false); Sequence.End(point); Sequence.Tick(); }
        public void Stop() { Sequence.CancelPending(); State.Stop(); }
        public void MoveTo(ScreenPoint point)
        {
            Calls.Add($"{Now}:move:{point.X},{point.Y}");
            if (FailMove) throw new InvalidOperationException("Move failed");
            Position = point;
        }
        public void LeftButtonDown()
        {
            Calls.Add($"{Now}:down");
            if (FailDown) throw new InvalidOperationException("Down failed");
            Now += DownCostMs;
        }
        public void LeftButtonUp()
        {
            Calls.Add($"{Now}:up");
            if (FailUp) throw new InvalidOperationException("Up failed");
        }
    }
}
