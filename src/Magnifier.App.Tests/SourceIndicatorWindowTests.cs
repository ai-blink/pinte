using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class SourceIndicatorWindowTests
{
    [TestMethod]
    public Task CaptureExclusionFailure_HidesAndRetainsErrorWithoutRetrying() => StaTest.Run(() =>
    {
        // EnsureHandle만 허용한다. fake 캡처 제외가 반드시 실패해 Show에는 도달하지 않는다.
        var capture = new NoDesktopCapture { ExclusionFailure = new InvalidOperationException("캡처 제외 거부") };
        var windows = new NoDesktopWindows();
        var indicator = new SourceIndicatorWindow(capture, windows);
        try
        {
            Assert.IsFalse(indicator.ShowRegion(new ScreenRegion(-400, 50, 320, 180)));
            Assert.IsFalse(indicator.IsVisible);
            Assert.IsNotNull(indicator.FailureReason);
            StringAssert.Contains(indicator.FailureReason!, "캡처 제외 거부");
            Assert.AreEqual(1, capture.ExclusionCount);
            Assert.AreEqual(1, windows.NativeOperationCount, "배치와 Show 전에 실패해야 한다.");
            var originalError = indicator.FailureReason;

            indicator.Hide();
            capture.ExclusionFailure = null;
            Assert.IsFalse(indicator.ShowRegion(new ScreenRegion(100, 200, 640, 360)));

            Assert.IsFalse(indicator.IsVisible);
            Assert.AreEqual(originalError, indicator.FailureReason);
            Assert.AreEqual(1, capture.ExclusionCount);
            Assert.AreEqual(1, windows.NativeOperationCount);
            Assert.AreEqual(0, capture.CaptureCount);
        }
        finally { indicator.Close(); }
        return Task.CompletedTask;
    });
}
