using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class LensCollapseOverlayWindowTests
{
    [TestMethod]
    public Task DefaultCaptureVisibility_DoesNotExcludeCollapseOverlay() => StaTest.Run(() =>
    {
        var capture = new NoDesktopCapture();
        var windows = new NoDesktopWindows();
        var overlay = new LensCollapseOverlayWindow(capture, windows);
        try
        {
            Assert.IsTrue(overlay.ShowAt(new ScreenPoint(400, 300)));
            CollectionAssert.AreEqual(new[] { false }, capture.ExclusionRequests);
            CollectionAssert.AreEqual(new[] { new ScreenRegion(374, 274, 52, 52) }, windows.TopmostPlacedBounds);
        }
        finally { overlay.Close(); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task CaptureExclusionFailure_HidesCollapseOverlayAndRetainsError() => StaTest.Run(() =>
    {
        var capture = new NoDesktopCapture { ExclusionFailure = new InvalidOperationException("캡처 제외 거부") };
        var overlay = new LensCollapseOverlayWindow(capture, new NoDesktopWindows(), hideFromScreenCapture: true);
        try
        {
            Assert.IsFalse(overlay.ShowAt(new ScreenPoint(400, 300)));
            Assert.IsFalse(overlay.IsVisible);
            Assert.IsNotNull(overlay.FailureReason);
            StringAssert.Contains(overlay.FailureReason!, "캡처 제외 거부");
        }
        finally { overlay.Close(); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DragMove_OffsetsTheIconWithoutChangingItsSize() => StaTest.Run(() =>
    {
        var windows = new NoDesktopWindows();
        var overlay = new LensCollapseOverlayWindow(new NoDesktopCapture(), windows);
        try
        {
            Assert.IsTrue(overlay.ShowAt(new ScreenPoint(400, 300)));
            var initialBounds = new ScreenRegion(374, 274, 52, 52);

            overlay.PlaceDraggedIcon(initialBounds, 120, -80);

            Assert.AreEqual(new ScreenRegion(494, 194, 52, 52), windows.TopmostPlacedBounds[^1]);
        }
        finally { overlay.Close(); }
        return Task.CompletedTask;
    });
}
