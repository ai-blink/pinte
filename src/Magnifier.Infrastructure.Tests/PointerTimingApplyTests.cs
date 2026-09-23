using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harness = Magnifier.Infrastructure.Tests.TimedPointerSequenceTests.Harness;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class PointerTimingApplyTests
{
    [TestMethod]
    public void Settings_BoundsRejectNegativeAndAboveSafetyLimit()
    {
        Assert.IsTrue(PointerTimingSettings.Default.IsValid());
        Assert.IsTrue(new PointerTimingSettings(0, 0, 0, 0).IsValid());
        Assert.IsTrue(new PointerTimingSettings(300, 200, 300, 300).IsValid());
        Assert.IsFalse(new PointerTimingSettings(-1, 35, 60, 0).IsValid());
        Assert.IsFalse(new PointerTimingSettings(301, 35, 60, 0).IsValid());
        Assert.IsFalse(new PointerTimingSettings(100, 201, 60, 0).IsValid());
        Assert.IsFalse(new PointerTimingSettings(100, 35, 301, 0).IsValid());
        Assert.IsFalse(new PointerTimingSettings(100, 35, 60, 301).IsValid());
        Assert.IsTrue(PointerTimingSettings.Presets.All(p => p.Value.IsValid()));
        Assert.AreEqual(PointerTimingSettings.Default, PointerTimingSettings.Presets[0].Value);
    }

    [TestMethod]
    public void StepFrom_UsesOneMillisecondBelowTenAndFiveAbove()
    {
        Assert.AreEqual(1, PointerTimingSettings.StepFrom(1, 1));
        Assert.AreEqual(1, PointerTimingSettings.StepFrom(9, 1));
        Assert.AreEqual(5, PointerTimingSettings.StepFrom(10, 1));
        Assert.AreEqual(-1, PointerTimingSettings.StepFrom(10, -1));
        Assert.AreEqual(-5, PointerTimingSettings.StepFrom(15, -1));
        Assert.AreEqual(-1, PointerTimingSettings.StepFrom(1, -1));
    }

    [TestMethod]
    public void Apply_IsRefusedWhileGestureIsCollectingOrInFlight()
    {
        var h = new Harness(new PointerTimingSettings());
        var fast = new PointerTimingSettings(20, 10, 15, 0);
        h.Begin(new(10, 20));
        Assert.IsFalse(h.Sequence.TryApplyTiming(fast), "Arrival must keep its snapshot.");
        h.At(100);
        Assert.IsFalse(h.Sequence.TryApplyTiming(fast), "Pressed gesture must keep its snapshot.");
        h.Now = 110; h.End(new(10, 20));
        h.At(135);
        Assert.IsFalse(h.Sequence.TryApplyTiming(fast), "Recovery must keep its snapshot.");
        h.At(195);
        Assert.IsFalse(h.Sequence.IsBusy);
        Assert.IsTrue(h.Sequence.TryApplyTiming(fast));
        Assert.AreEqual(fast, h.Sequence.Timing);
    }

    [TestMethod]
    public void AppliedTiming_ChangesOnlyTheNextGesture()
    {
        var h = new Harness(new PointerTimingSettings());
        Assert.IsTrue(h.Sequence.TryApplyTiming(new(20, 10, 15, 0)));
        h.Begin(new(10, 20)); h.End(new(10, 20));
        for (var budget = 0; h.Sequence.NextWakeAt is { } due && budget < 100; budget++) h.At(Math.Max(h.Now, due));
        CollectionAssert.AreEqual(new[] { "0:move:10,20", "20:down", "30:move:10,20", "30:up", "45:move:900,800" }, h.Calls);
    }

    [TestMethod]
    public void Apply_RejectsNegativeValuesWithoutChangingSnapshot()
    {
        var h = new Harness(new PointerTimingSettings());
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => h.Sequence.TryApplyTiming(new(-5, 0, 0, 0)));
        Assert.AreEqual(new PointerTimingSettings(), h.Sequence.Timing);
    }
}
