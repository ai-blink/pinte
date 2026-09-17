using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class LensCollapseOverlayWindowTests
{
    [TestMethod]
    public Task CaptureExclusionFailure_HidesCollapseOverlayAndRetainsError() => StaTest.Run(() =>
    {
        var capture = new NoDesktopCapture { ExclusionFailure = new InvalidOperationException("캡처 제외 거부") };
        var overlay = new LensCollapseOverlayWindow(capture, new NoDesktopWindows());
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
}
