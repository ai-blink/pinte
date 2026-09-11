using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class PointerCaptureMonitorTests
{
    [TestMethod]
    public void ForegroundChange_CannotSubstituteOtherThreadsCapture()
    {
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        CaptureSample Read(uint thread, nint root)
        {
            Assert.AreEqual(42u, thread, "Use the source thread even when foreground changes.");
            Assert.AreEqual((nint)100, root);
            return new(true, 101, 100, 42, true, FocusMovedAway: true);
        }
        Assert.IsFalse(monitor.LostCapture(Read, () => false));
        Assert.IsFalse(monitor.LostCapture(Read, () => false));
    }

    [TestMethod]
    public void SameThreadCaptureHandoff_KeepsContinuousDragWithoutExtraUp()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        var monitor = new PointerCaptureMonitor();
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        monitor.Begin(42, 100);
        foreach (var (capture, root) in new (nint, nint)[] { (101, 100), (901, 900), (100, 100), (101, 100) })
        {
            if (monitor.LostCapture((_, _) => new(true, capture, root, 42, true, false), () => false)) state.Stop();
            state.Move(new((int)capture, 30));
        }
        Assert.IsTrue(state.IsPressed);
        Assert.IsTrue(state.IsRequested);
        Assert.AreEqual(0, input.Ups);
        state.ObservePhysicalButton(false);
        state.Complete(new(101, 30));
        Assert.AreEqual(1, input.Ups);
        state.Pause();
        Assert.IsTrue(state.TryResume(false), "The next operation remains available after normal Up.");
    }

    [TestMethod]
    public void NeverCapturedOrUnrelatedCapture_IsNotTargetCaptureLoss()
    {
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        Assert.IsFalse(monitor.LostCapture((_, _) => new(true, 0, 0, 0, true, false), () => false));
        Assert.IsFalse(monitor.LostCapture((_, _) => new(true, 901, 900, 99, true, false), () => false));
        Assert.IsFalse(monitor.LostCapture((_, _) => new(true, 0, 0, 0, true, false), () => false));
    }

    [TestMethod]
    public void CaptureReleasedWithinActiveSource_KeepsMovesUntilPhysicalUp()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        Assert.IsFalse(monitor.LostCapture((_, _) => new(true, 101, 100, 42, true, false), () => false));
        // DragDetect can finish before button Up; target applications need not recapture.
        foreach (var point in new ScreenPoint[] { new(15, 25), new(18, 23), new(11, 21) })
        {
            if (monitor.LostCapture((_, _) => new(true, 0, 0, 0, true, false), () => false)) state.Stop();
            state.Move(point);
        }
        Assert.IsTrue(state.IsPressed);
        Assert.IsTrue(state.IsRequested);
        Assert.AreEqual(0, input.Ups);
        CollectionAssert.AreEqual(new ScreenPoint[] { new(10, 20), new(15, 25), new(18, 23), new(11, 21) }, input.Moves);
        state.ObservePhysicalButton(false);
        state.Complete(new(11, 21));
        Assert.AreEqual(1, input.Ups);
    }

    [TestMethod]
    public void ConfirmedTargetCaptureLoss_ReleasesAndCancelsRequest()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        Assert.IsFalse(monitor.LostCapture((_, _) => new(true, 101, 100, 42, true, false), () => false));
        if (monitor.LostCapture((_, _) => new(true, 0, 0, 0, true, FocusMovedAway: true), () => false)) state.Stop();
        Assert.IsFalse(state.IsPressed);
        Assert.IsFalse(state.IsRequested);
        Assert.AreEqual(1, input.Ups);
        state.ObservePhysicalButton(false);
        Assert.IsFalse(state.TryResume(false));
    }

    [TestMethod]
    public void PendingUpDuringRead_WinsOverCaptureLoss()
    {
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        Assert.IsFalse(monitor.LostCapture((_, _) => new(true, 101, 100, 42, true, false), () => false));
        var pending = false;
        Assert.IsFalse(monitor.LostCapture((_, _) =>
        {
            pending = true;
            return new(true, 0, 0, 0, true, FocusMovedAway: true);
        }, () => pending));
    }

    [TestMethod]
    public void ClosedSourceAndTransferOutsideThread_AreStillLosses()
    {
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        Assert.IsTrue(monitor.LostCapture((_, _) => new(false, 0, 0, 0, true, false), () => false));
        Assert.IsTrue(monitor.LostCapture((_, _) => new(true, 0, 0, 0, TargetAvailable: false, false), () => false));
        Assert.IsFalse(monitor.LostCapture((_, _) => new(true, 101, 100, 42, true, false), () => false));
        Assert.IsTrue(monitor.LostCapture((_, _) => new(true, 901, 900, 99, true, false), () => false));
        monitor.Reset();
        Assert.IsFalse(monitor.LostCapture((_, _) => throw new Exception("Must not sample after end"), () => false));
    }

    private sealed class RecordingInput : IPointerInput
    {
        public int Ups { get; private set; }
        public List<ScreenPoint> Moves { get; } = [];
        public void MoveTo(ScreenPoint point) => Moves.Add(point);
        public void LeftButtonDown() { }
        public void LeftButtonUp() => Ups++;
    }
}
