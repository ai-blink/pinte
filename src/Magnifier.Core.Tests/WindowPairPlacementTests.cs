using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class WindowPairPlacementTests
{
    [TestMethod]
    public void Lower_UsesFullWindowBoundsAndSeparatesWindows()
    {
        var work = new ScreenRegion(0, 0, 2560, 1400);
        var (frame, lens) = WindowPairPlacement.Lower(work,
            new(0, 0, 500, 440), new(0, 0, 1000, 640));

        Assert.IsTrue(frame.Y > work.Height / 3);
        Assert.AreEqual(frame.Y, lens.Y);
        Assert.IsTrue(frame.X + frame.Width < lens.X);
        Assert.AreEqual(500, frame.Width);
        Assert.AreEqual(1000, lens.Width);
        Inside(work, frame); Inside(work, lens);
    }

    [TestMethod]
    public void Lower_PreservesNegativeMonitorOriginAndPhysicalHighDpiSizes()
    {
        var work = new ScreenRegion(-3000, -1400, 3000, 1360);
        var (frame, lens) = WindowPairPlacement.Lower(work,
            new(0, 0, 760, 620), new(0, 0, 1520, 920));

        Assert.AreEqual(760, frame.Width);
        Assert.AreEqual(1520, lens.Width);
        Assert.IsTrue(frame.X < 0 && lens.Y < 0);
        Inside(work, frame); Inside(work, lens);
    }

    [TestMethod]
    public void Lower_NarrowWorkAreaKeepsBothWindowsInside()
    {
        var work = new ScreenRegion(100, 200, 700, 1000);
        var (frame, lens) = WindowPairPlacement.Lower(work,
            new(0, 0, 500, 250), new(0, 0, 900, 450));
        Assert.IsTrue(frame.Y + frame.Height < lens.Y);
        Inside(work, frame); Inside(work, lens);
    }

    private static void Inside(ScreenRegion work, ScreenRegion window)
    {
        Assert.IsTrue(window.X >= work.X && window.Y >= work.Y);
        Assert.IsTrue(window.X + window.Width <= work.X + work.Width);
        Assert.IsTrue(window.Y + window.Height <= work.Y + work.Height);
    }
}
