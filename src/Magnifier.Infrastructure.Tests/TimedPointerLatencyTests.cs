using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harness = Magnifier.Infrastructure.Tests.TimedPointerSequenceTests.Harness;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class TimedPointerLatencyTests
{
    [TestMethod]
    public void Defaults_KeepArrivalAndHoldButRemoveLongRecovery()
    {
        var timing = new PointerTimingSettings();
        Assert.AreEqual(100, timing.ArrivalMs);
        Assert.AreEqual(35, timing.MinimumHoldMs);
        Assert.AreEqual(60, timing.PostReleaseMs);
        Assert.AreEqual(0, timing.BetweenGesturesMs);
        var h = new Harness(timing);
        h.Begin(new(10, 20)); h.Now = 10; h.End(new(10, 20));
        DrainScheduled(h);
        Assert.AreEqual(195L, h.Now);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down", "135:move:10,20",
            "135:up", "195:move:900,800" }, h.Calls);
        Assert.IsFalse(h.Sequence.IsBusy);
    }

    [TestMethod]
    public void ZeroArrival_PressesWithoutWaitingForAnotherMessage()
    {
        var h = new Harness(new(0, 35, 60, 0));
        h.Begin(new(10, 20));
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "0:down" }, h.Calls);
        Assert.IsTrue(h.State.IsPressed);
        Assert.IsNull(h.Sequence.NextWakeAt, "Holding still needs no sequence polling.");
    }

    [TestMethod]
    public void AllZeroWaits_ReleaseRestoreAndFinishInSameMessage()
    {
        var h = new Harness(new(0, 0, 0, 0));
        h.Begin(new(10, 20)); h.Now = 10; h.End(new(10, 20));
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "0:down", "10:move:10,20",
            "10:up", "10:move:900,800" }, h.Calls);
        Assert.IsFalse(h.Sequence.IsBusy);
        Assert.IsFalse(h.State.IsPressed);
        Assert.IsNull(h.Sequence.NextWakeAt);
    }

    [TestMethod]
    public void ThreeQueuedTaps_Have195msIdealDownIntervals()
    {
        var h = new Harness(new PointerTimingSettings());
        for (var i = 0; i < 3; i++)
        {
            h.Now = i * 20; h.Begin(new(10 + i, 20 + i), i + 1);
            h.Now += 10; h.End(new(10 + i, 20 + i));
        }
        DrainScheduled(h);
        CollectionAssert.AreEqual(new[] { "100:down", "295:down", "490:down" },
            h.Calls.Where(x => x.EndsWith(":down")).ToArray());
        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, h.PressedAttempts);
        Assert.AreEqual(585L, h.Now);
    }

    [TestMethod]
    public void MaximumDragBacklog_YieldsEveryEightMovesWithoutTimerTail()
    {
        var h = new Harness(new PointerTimingSettings());
        h.Begin(new(10, 20));
        for (var i = 1; i <= TimedPointerSequence.MaximumBufferedMoves; i++)
            h.Sequence.Move(new(10 + i, 20 + i));
        h.Now = 10; h.End(new(522, 532));
        h.At(100);
        Assert.AreEqual(10, h.Calls.Count); // prepare, Down, eight moves
        Assert.AreEqual(100L, h.Sequence.NextWakeAt);
        var calls = h.Calls.Count;
        h.At(100);
        Assert.AreEqual(8, h.Calls.Count - calls);
        DrainScheduled(h);
        var expected = new List<string> { "0:move:10,20", "100:down" };
        expected.AddRange(Enumerable.Range(1, 512).Select(i => $"100:move:{10 + i},{20 + i}"));
        expected.AddRange(["135:move:522,532", "135:up", "195:move:900,800"]);
        CollectionAssert.AreEqual(expected, h.Calls);
        Assert.AreEqual(195L, h.Now);
    }

    [TestMethod]
    public void DragWithRealHold_WaitsForUserUpThenOnlyRecovery()
    {
        var h = new Harness(new PointerTimingSettings());
        h.Begin(new(10, 20)); h.At(100);
        h.Now = 200; h.Move(new(30, 40));
        Assert.IsNull(h.Sequence.NextWakeAt);
        h.Now = 1000; h.End(new(40, 50));
        Assert.AreEqual("1000:up", h.Calls.Last());
        Assert.AreEqual(1060L, h.Sequence.NextWakeAt);
        DrainScheduled(h);
        Assert.AreEqual(1060L, h.Now);
    }

    [TestMethod]
    public void StopBeforeQueuedContinuation_DropsBacklogAndReleasesOnce()
    {
        var h = new Harness(new PointerTimingSettings());
        h.Begin(new(10, 20));
        for (var i = 0; i < 20; i++) h.Sequence.Move(new(11 + i, 21 + i));
        h.At(100);
        Assert.AreEqual(100L, h.Sequence.NextWakeAt);
        var pump = new RelayCommandPump();
        pump.Enqueue(h.Stop);
        pump.ProcessMessage(timer: false, () => Assert.Fail());
        var count = h.Calls.Count;
        h.Sequence.Tick(); // Already-posted continuation arrives after Stop.
        Assert.AreEqual(count, h.Calls.Count);
        Assert.AreEqual(1, h.Calls.Count(x => x.EndsWith(":up")));
        Assert.IsNull(h.Sequence.NextWakeAt);
        Assert.IsFalse(h.State.IsPressed);
    }

    [TestMethod]
    public void FailedUp_LeavesReleaseResponsibilityAndNoWake()
    {
        var h = new Harness(new PointerTimingSettings());
        h.Begin(new(10, 20)); h.End(new(10, 20)); h.At(100);
        h.FailUp = true;
        Assert.ThrowsException<InvalidOperationException>(() => h.At(135));
        Assert.IsTrue(h.State.IsPressed);
        Assert.IsNull(h.Sequence.NextWakeAt);
        Assert.AreEqual(0, h.Restores);
        h.FailUp = false; h.Stop();
    }

    [TestMethod]
    public void ZeroWaitQueue_RemainsBoundedAndPreservesEveryGesture()
    {
        var h = new Harness(new(0, 0, 0, 0));
        for (var i = 0; i < TimedPointerSequence.MaximumGestures; i++)
        {
            h.Sequence.Begin(new(i, i), i);
            h.Sequence.End(new(i, i));
        }
        h.Sequence.Tick();
        Assert.AreEqual(8, h.Calls.Count(x => x.EndsWith(":down")));
        Assert.AreEqual(8, h.Calls.Count(x => x.EndsWith(":up")));
        Assert.AreEqual(8, h.Restores);
        Assert.IsNull(h.Sequence.NextWakeAt);
    }

    [TestMethod]
    public void ZeroWaitQueuedUp_ClearsReleaseGateAfterCommandDrain()
    {
        var h = new Harness(new(0, 0, 0, 0));
        var pump = new RelayCommandPump();
        h.Begin(new(10, 20));
        pump.Enqueue(() =>
        {
            h.End(new(10, 20));
            Assert.IsFalse(h.Sequence.IsBusy);
            Assert.IsTrue(pump.HasPendingLeftRelease);
        }, leftRelease: true);
        pump.ProcessMessage(timer: false, () => Assert.Fail());
        Assert.IsFalse(pump.HasPendingLeftRelease);
        Assert.IsFalse(h.State.IsPressed);
        Assert.IsNull(h.Sequence.NextWakeAt);
    }

    [TestMethod]
    public void LegacyProfile_With50msPollingRetains800msBaseline()
    {
        var h = new Harness(new(100, 35, 560, 50));
        h.Begin(new(10, 20)); h.Now = 10; h.End(new(10, 20));
        for (var time = 50; h.Sequence.IsBusy && time <= 1000; time += 50) h.At(time);
        Assert.AreEqual(800L, h.Now);
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "100:down", "150:move:10,20",
            "150:up", "750:move:900,800" }, h.Calls);
    }

    private static void DrainScheduled(Harness h)
    {
        var budget = 1000;
        while (h.Sequence.NextWakeAt is { } due && budget-- > 0)
            h.At(Math.Max(h.Now, due));
        Assert.IsTrue(budget > 0, "Sequence must not spin without bounded progress.");
    }
}
