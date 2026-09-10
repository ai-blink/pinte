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
        CaptureSample Read(uint thread)
        {
            Assert.AreEqual(42u, thread, "Use the source thread even when foreground changes.");
            return thread == 42 ? new(true, 101, 100) : new(true, 0, 0);
        }
        Assert.IsFalse(monitor.LostCapture(Read, () => false));
        Assert.IsFalse(monitor.LostCapture(Read, () => false));
    }

    [TestMethod]
    public void SameRootCaptureHandoff_KeepsContinuousDragWithoutExtraUp()
    {
        var input = new RecordingInput();
        var state = new LensInputState(input);
        var monitor = new PointerCaptureMonitor();
        state.Arm(false);
        state.ObservePhysicalButton(true);
        state.Begin(new(10, 20));
        monitor.Begin(42, 100);
        foreach (var capture in new nint[] { 101, 102, 100, 101 })
        {
            if (monitor.LostCapture(_ => new(true, capture, 100), () => false)) state.Stop();
            state.Move(new((int)capture, 30));
        }
        Assert.IsTrue(state.IsPressed);
        Assert.IsTrue(state.IsRequested);
        Assert.AreEqual(0, input.Ups);
        state.ObservePhysicalButton(false);
        state.Complete(new(101, 30));
        Assert.AreEqual(1, input.Ups);
    }

    [TestMethod]
    public void NeverCapturedOrUnrelatedCapture_IsNotTargetCaptureLoss()
    {
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        Assert.IsFalse(monitor.LostCapture(_ => new(true, 0, 0), () => false));
        Assert.IsFalse(monitor.LostCapture(_ => new(true, 901, 900), () => false));
        Assert.IsFalse(monitor.LostCapture(_ => new(true, 0, 0), () => false));
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
        Assert.IsFalse(monitor.LostCapture(_ => new(true, 101, 100), () => false));
        if (monitor.LostCapture(_ => new(true, 0, 0), () => false)) state.Stop();
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
        Assert.IsFalse(monitor.LostCapture(_ => new(true, 101, 100), () => false));
        var pending = false;
        Assert.IsFalse(monitor.LostCapture(_ => { pending = true; return new(true, 0, 0); }, () => pending));
    }

    [TestMethod]
    public void ClosedThreadAndTransferOutsideRoot_AreStillLosses()
    {
        var monitor = new PointerCaptureMonitor();
        monitor.Begin(42, 100);
        Assert.IsTrue(monitor.LostCapture(_ => new(false, 0, 0), () => false));
        Assert.IsFalse(monitor.LostCapture(_ => new(true, 101, 100), () => false));
        Assert.IsTrue(monitor.LostCapture(_ => new(true, 901, 900), () => false));
        monitor.Reset();
        Assert.IsFalse(monitor.LostCapture(_ => throw new Exception("Must not sample after end"), () => false));
    }

    private sealed class RecordingInput : IPointerInput
    {
        public int Ups { get; private set; }
        public void MoveTo(ScreenPoint point) { }
        public void LeftButtonDown() { }
        public void LeftButtonUp() => Ups++;
    }
}
