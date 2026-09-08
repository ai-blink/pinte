using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Core.Tests;

[TestClass]
public class PreviewCoordinateMapperTests
{
    [TestMethod]
    public void MapToScreen_MapsViewportPointToPhysicalScreenPixel()
    {
        var region = new ScreenRegion(120, 80, 400, 200);

        var mapped = PreviewCoordinateMapper.MapToScreen(
            region,
            new PreviewSize(800, 400),
            new PreviewPoint(300, 100));

        Assert.AreEqual(new ScreenPoint(270, 130), mapped);
    }

    [TestMethod]
    public void MapToScreen_ClampsViewportEdgesToTheCapturedRegion()
    {
        var region = new ScreenRegion(-50, 20, 4, 3);
        var previewSize = new PreviewSize(40, 30);

        var beforeStart = PreviewCoordinateMapper.MapToScreen(region, previewSize, new PreviewPoint(-10, -1));
        var afterEnd = PreviewCoordinateMapper.MapToScreen(region, previewSize, new PreviewPoint(40, 30));

        Assert.AreEqual(new ScreenPoint(-50, 20), beforeStart);
        Assert.AreEqual(new ScreenPoint(-47, 22), afterEnd);
    }

    [TestMethod]
    public void CapturedFrame_RejectsUnexpectedPixelBufferLength()
    {
        var region = new ScreenRegion(0, 0, 2, 2);

        Assert.ThrowsException<ArgumentException>(() => new CapturedFrame(region, new byte[15]));
    }
}
