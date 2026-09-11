using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public sealed class LensViewportTests
{
    [TestMethod]
    public void SavedSource_RoundTripsPhysicalRegionIncludingNegativeOrigin()
    {
        var region = new ScreenRegion(-1530, -420, 371, 223);
        var json = System.Text.Json.JsonSerializer.Serialize(region);
        Assert.AreEqual(region, System.Text.Json.JsonSerializer.Deserialize<ScreenRegion>(json));
    }
    [TestMethod]
    public void DestinationTranslation_PreservesSourceAndMatchingImagePoint()
    {
        var original = new LensViewport(new ScreenRegion(20, 30, 100, 80), new ScreenRegion(400, 500, 500, 400));
        var moved = original with { Destination = new ScreenRegion(-900, -250, 500, 400) };

        Assert.AreEqual(original.Source, moved.Source);
        Assert.AreEqual(new ScreenPoint(70, 70), original.MapToSource(new PreviewPoint(650, 700)));
        Assert.AreEqual(original.MapToSource(new PreviewPoint(650, 700)), moved.MapToSource(new PreviewPoint(-650, -50)));
    }

    [TestMethod]
    public void PhysicalPixelMapping_HandlesNegativeCoordinatesAndDifferentMonitorScales()
    {
        // 각 모니터의 DIP를 물리 픽셀로 변환한 뒤 비정수 배율을 적용한 영역.
        var viewport = new LensViewport(new ScreenRegion(-1800, -300, 601, 301), new ScreenRegion(150, -900, 1125, 562));

        Assert.AreEqual(new ScreenPoint(-1500, -150), viewport.MapToSource(new PreviewPoint(712.5, -619)));
        Assert.AreEqual(new ScreenPoint(-1800, -300), viewport.MapToSource(new PreviewPoint(149, -901)));
        Assert.AreEqual(new ScreenPoint(-1200, 0), viewport.MapToSource(new PreviewPoint(1275, -338)));
    }

    [TestMethod]
    public void Contains_ExcludesRightAndBottomBoundaryAndSurroundingControls()
    {
        var viewport = new LensViewport(new ScreenRegion(0, 0, 50, 50), new ScreenRegion(-100, -50, 200, 100));

        Assert.IsTrue(viewport.Contains(new ScreenPoint(-100, -50)));
        Assert.IsTrue(viewport.Contains(new ScreenPoint(99, 49)));
        Assert.IsFalse(viewport.Contains(new ScreenPoint(100, 0)));
        Assert.IsFalse(viewport.Contains(new ScreenPoint(0, 50)));
        Assert.IsFalse(viewport.Contains(new ScreenPoint(0, -51)));
    }

    [TestMethod]
    public void CroppedSource_MapsFixedLensViewportToVisibleCenterOfOriginal()
    {
        // The outer lens can stay fixed while its scaled image is clipped to this source crop.
        var viewport = new LensViewport(new ScreenRegion(125, 220, 150, 100),
            new ScreenRegion(400, 500, 600, 400));

        Assert.AreEqual(new ScreenPoint(125, 220), viewport.MapToSource(new PreviewPoint(400, 500)));
        Assert.AreEqual(new ScreenPoint(200, 270), viewport.MapToSource(new PreviewPoint(700, 700)));
        Assert.AreEqual(new ScreenPoint(274, 319), viewport.MapToSource(new PreviewPoint(999, 899)));
    }
}
