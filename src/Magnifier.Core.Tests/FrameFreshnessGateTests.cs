using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class FrameFreshnessGateTests
{
    [TestMethod]
    public void RequireNextFrame_InvalidatesAFrameRecordedBeforeTheBoundary()
    {
        var gate = new FrameFreshnessGate();
        gate.RecordFrame(1000);

        Assert.IsTrue(gate.IsFresh(1100, 750));

        gate.RequireNextFrame();

        Assert.IsFalse(gate.IsFresh(1100, 750));
        gate.RecordFrame(1120);
        Assert.IsTrue(gate.IsFresh(1200, 750));
    }

    [TestMethod]
    public void IsFresh_RejectsExpiredFrames()
    {
        var gate = new FrameFreshnessGate();
        gate.RecordFrame(1000);

        Assert.IsFalse(gate.IsFresh(1750, 750));
    }
}
