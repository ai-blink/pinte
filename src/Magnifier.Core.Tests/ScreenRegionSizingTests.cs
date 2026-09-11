using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class ScreenRegionSizingTests
{
    private static readonly ScreenRegion Desktop = new(-1600, -900, 3200, 1800);

    [TestMethod]
    public void CornerResize_WithLockedAspect_KeepsOppositeCornerAndRatio()
    {
        var current = new ScreenRegion(-400, 100, 320, 180);

        var resized = ScreenRegionSizing.Resize(current, RegionResizeHandle.NorthWest,
            -160, -90, Desktop, lockedAspectRatio: 16d / 9d);

        Assert.AreEqual(-80, resized.X + resized.Width);
        Assert.AreEqual(280, resized.Y + resized.Height);
        Assert.AreEqual(resized.Width / (double)resized.Height, 16d / 9d, 0.01);
    }

    [TestMethod]
    public void SideResize_WithLockedAspect_KeepsOppositeSideCenter()
    {
        var current = new ScreenRegion(100, 100, 400, 300);

        var resized = ScreenRegionSizing.Resize(current, RegionResizeHandle.East,
            200, 0, Desktop, lockedAspectRatio: 4d / 3d);

        Assert.AreEqual(100, resized.X);
        Assert.AreEqual(250, resized.Y + resized.Height / 2);
        Assert.AreEqual(resized.Width / (double)resized.Height, 4d / 3d, 0.01);
    }

    [TestMethod]
    public void Fit_PreservesNegativeVirtualDesktopCoordinates()
    {
        var fitted = ScreenRegionSizing.Fit(new ScreenRegion(-1900, -1000, 900, 600), Desktop);

        Assert.AreEqual(new ScreenRegion(-1600, -900, 900, 600), fitted);
    }

    [TestMethod]
    public void Resize_WithoutLockedAspect_StaysWithinVirtualDesktop()
    {
        var current = new ScreenRegion(1300, 700, 220, 160);

        var resized = ScreenRegionSizing.Resize(current, RegionResizeHandle.SouthEast,
            1000, 1000, Desktop);

        Assert.AreEqual(1300, resized.X);
        Assert.AreEqual(700, resized.Y);
        Assert.AreEqual(1600, resized.X + resized.Width);
        Assert.AreEqual(900, resized.Y + resized.Height);
    }
}
