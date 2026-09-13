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

    [DataTestMethod]
    [DataRow(1d, 0.25)] [DataRow(1.25, 0.25)] [DataRow(1.5, 0.25)] [DataRow(2d, 0.25)]
    [DataRow(1d, 1.375)] [DataRow(1.25, 1.375)] [DataRow(1.5, 1.375)] [DataRow(2d, 1.375)]
    [DataRow(1d, 2.5)] [DataRow(1.25, 2.5)] [DataRow(1.5, 2.5)] [DataRow(2d, 2.5)]
    [DataRow(1d, 8d)] [DataRow(1.25, 8d)] [DataRow(1.5, 8d)] [DataRow(2d, 8d)]
    public void CropAcrossDpiAndZoom_MapsCenterAndCornersWithinOneSourcePixel(double dpi, double zoom)
    {
        var source = new ScreenRegion(-1800, -470, 601, 301);
        var origin = new PreviewPoint(-723.35 * dpi, 107.6 * dpi);
        // App과 마찬가지로 물리 zoom을 DIP로 배치한 뒤 화면 좌표를 측정한다.
        var rendered = new PreviewSize(source.Width * zoom / dpi * dpi, source.Height * zoom / dpi * dpi);
        var visibleOrigin = new PreviewPoint(origin.X + rendered.Width * 0.19, origin.Y + rendered.Height * 0.13);
        var visibleSize = new PreviewSize(rendered.Width * 0.58, rendered.Height * 0.67);
        Assert.IsTrue(LensViewport.TryCreate(source, origin, rendered, visibleOrigin, visibleSize, out var viewport));

        foreach (var fractionX in new[] { 0, 0.5, 1 })
        foreach (var fractionY in new[] { 0, 0.5, 1 })
        {
            var x = viewport.Destination.X + Math.Floor((viewport.Destination.Width - 1) * fractionX);
            var y = viewport.Destination.Y + Math.Floor((viewport.Destination.Height - 1) * fractionY);
            var mapped = viewport.MapToSource(new PreviewPoint(x, y));
            var expectedX = source.X + Math.Clamp((int)Math.Floor((x - origin.X) / zoom), 0, source.Width - 1);
            var expectedY = source.Y + Math.Clamp((int)Math.Floor((y - origin.Y) / zoom), 0, source.Height - 1);
            Assert.IsTrue(Math.Abs(mapped.X - expectedX) <= 1, $"X 오차: dpi={dpi}, zoom={zoom}");
            Assert.IsTrue(Math.Abs(mapped.Y - expectedY) <= 1, $"Y 오차: dpi={dpi}, zoom={zoom}");
            Assert.IsTrue(viewport.Contains(new ScreenPoint((int)x, (int)y)));
        }
        Assert.IsFalse(viewport.Contains(new ScreenPoint(viewport.Destination.X + viewport.Destination.Width, viewport.Destination.Y)));
        Assert.IsFalse(viewport.Contains(new ScreenPoint(viewport.Destination.X, viewport.Destination.Y + viewport.Destination.Height)));
    }

    [TestMethod]
    public void ResizeViewport_PreservesSourcePointWhileCropChanges()
    {
        var source = new ScreenRegion(-200, 80, 321, 181);
        var origin = new PreviewPoint(25.4, -44.2);
        var imageSize = new PreviewSize(321 * 1.375, 181 * 1.375);
        Assert.IsTrue(LensViewport.TryCreate(source, origin, imageSize, new PreviewPoint(70, 0),
            new PreviewSize(280, 180), out var large));
        Assert.IsTrue(LensViewport.TryCreate(source, origin, imageSize, new PreviewPoint(120, 20),
            new PreviewSize(170, 120), out var small));
        var pointer = new PreviewPoint(180, 70);
        Assert.AreEqual(large.MapToSource(pointer), small.MapToSource(pointer));
        Assert.AreNotEqual(large.Source, small.Source);
    }

    [TestMethod]
    public void SubPixelImageOrigin_IsNotRoundedBeforeMappingAtQuarterZoom()
    {
        var source = new ScreenRegion(10, 20, 321, 181);
        Assert.IsTrue(LensViewport.TryCreate(source, new PreviewPoint(-0.9, -0.9),
            new PreviewSize(80.25, 45.25), new PreviewPoint(0, 0), new PreviewSize(50, 30), out var viewport));
        Assert.AreEqual(new ScreenPoint(13, 23), viewport.MapToSource(new PreviewPoint(0, 0)));
        Assert.AreEqual(new ScreenPoint(133, 103), viewport.MapToSource(new PreviewPoint(30, 20)));
    }

    [TestMethod]
    public void LetterboxAndNonOverlappingRegions_DoNotBecomeInputTargets()
    {
        var source = new ScreenRegion(0, 0, 80, 60);
        Assert.IsTrue(LensViewport.TryCreate(source, new PreviewPoint(20, 30), new PreviewSize(80, 60),
            new PreviewPoint(0, 0), new PreviewSize(200, 200), out var viewport));
        Assert.AreEqual(new ScreenRegion(20, 30, 80, 60), viewport.Destination);
        Assert.IsFalse(viewport.Contains(new ScreenPoint(19, 30)));
        Assert.IsFalse(LensViewport.TryCreate(source, new PreviewPoint(0, 0), new PreviewSize(80, 60),
            new PreviewPoint(80, 60), new PreviewSize(20, 20), out _));
        Assert.IsFalse(LensViewport.TryCreate(source, new PreviewPoint(0, 0), new PreviewSize(double.NaN, 60),
            new PreviewPoint(0, 0), new PreviewSize(20, 20), out _));
    }
}
