using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class FirstFrameTests
{
    [TestMethod]
    public Task FirstCapture_BootstrapsImageLayoutWhileGeometryIsPending() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var source = new ScreenRegion(20, 30, 80, 60);
        await host.Lens.SetSourceAsync(source, 0);
        var image = (Image)host.Lens.FindName("CapturedImage");
        var emptyText = (TextBlock)host.Lens.FindName("EmptyImageText");
        ArrangeImage(image);
        Assert.IsNull(image.Source);
        Assert.AreEqual(0d, image.ActualWidth, "Source 없는 Image는 Width를 지정해도 렌더링 크기가 0이다.");
        PrivateAccess.Set(host.Lens, "_geometryPending", true);

        host.Lens.UpdateCapture(CreateFrame(source, 73), true);
        ArrangeImage(image);

        Assert.IsInstanceOfType<WriteableBitmap>(image.Source);
        Assert.IsTrue(image.ActualWidth > 0 && image.ActualHeight > 0,
            "첫 캡처가 Image에 들어가야 이후 좌표 계산을 시작할 수 있다.");
        Assert.AreEqual(Visibility.Collapsed, emptyText.Visibility);
        Assert.AreEqual(73, ReadFirstPixel(image));
        Assert.IsTrue(PrivateAccess.Get<bool>(host.Lens, "_geometryPending"));
        Assert.AreEqual(0, host.Relay.FrameCount, "표시만 갱신하며 아직 입력 재개 프레임으로 인정하지 않는다.");
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.AreEqual(0, host.Pointer.CallCount);
    });

    [TestMethod]
    public Task GeometryCompletion_RequiresAnotherCaptureForRelayFreshness() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var source = new ScreenRegion(20, 30, 80, 60);
        await host.Lens.SetSourceAsync(source, 0);
        PrivateAccess.Set(host.Lens, "_geometryPending", true);
        host.Lens.UpdateCapture(CreateFrame(source, 11), true);
        Assert.AreEqual(0, host.Relay.FrameCount);

        // HWND 없는 테스트에서는 geometry 완료 경계만 설정한다. 실제 좌표 통합은 사용자 확인 대상이다.
        PrivateAccess.Set(host.Lens, "_geometryPending", false);
        Assert.AreEqual(0, host.Relay.FrameCount, "먼저 표시한 프레임을 나중에 소급 인정하면 안 된다.");
        host.Lens.UpdateCapture(CreateFrame(source, 99), true);

        Assert.AreEqual(99, ReadFirstPixel((Image)host.Lens.FindName("CapturedImage")));
        Assert.AreEqual(1, host.Relay.FrameCount);
        Assert.AreEqual(0, host.Relay.StartCount);
    });

    [TestMethod]
    public Task PendingGeometry_DoesNotAcceptFrameFromPreviousSource() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var current = new ScreenRegion(20, 30, 80, 60);
        await host.Lens.SetSourceAsync(current, 0);
        PrivateAccess.Set(host.Lens, "_geometryPending", true);

        host.Lens.UpdateCapture(CreateFrame(new ScreenRegion(40, 50, 80, 60), 1), true);

        Assert.IsNull(((Image)host.Lens.FindName("CapturedImage")).Source);
        Assert.AreEqual(0, host.Relay.FrameCount);
    });

    [TestMethod]
    public Task ViewportUnavailable_RetriesGeometryThenAcceptsOnlyANewerFrame() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var lens = host.Lens;
        var source = new ScreenRegion(20, 30, 80, 60);
        PrepareOffscreenWindow(lens);
        lens.Show();
        await Dispatcher.Yield(DispatcherPriority.Loaded);

        var image = (Image)lens.FindName("CapturedImage");
        image.Visibility = Visibility.Collapsed;
        lens.UpdateLayout();
        await lens.SetSourceAsync(source, (nint)1);
        lens.UpdateCapture(CreateFrame(source, 47), true);

        Assert.IsTrue(PrivateAccess.Get<bool>(lens, "_geometryPending"),
            "일시적으로 표시 viewport를 계산할 수 없으면 새 프레임을 입력 재개 근거로 쓰면 안 된다.");
        var retryTimer = PrivateAccess.Get<DispatcherTimer?>(lens, "_geometryRetryTimer");
        Assert.IsNotNull(retryTimer, "viewport 실패는 후속 배치 재시도를 예약해야 한다.");
        Assert.IsTrue(retryTimer.IsEnabled);
        Assert.AreEqual(0, host.Relay.FrameCount);

        image.Visibility = Visibility.Visible;
        lens.UpdateLayout();
        await WaitUntilAsync(() => !PrivateAccess.Get<bool>(lens, "_geometryPending"),
            "viewport가 다시 유효해진 뒤 geometry 대기가 해제되지 않았다.");

        Assert.AreEqual(0, host.Relay.FrameCount,
            "geometry 완료 전 표시된 프레임을 나중에 소급해 입력 재개 근거로 쓰면 안 된다.");
        lens.UpdateCapture(CreateFrame(source, 93), true);

        Assert.AreEqual(1, host.Relay.FrameCount,
            "재시도로 geometry가 완료되면 그 다음 캡처가 freshness를 갱신해야 한다.");
    });

    [TestMethod]
    public Task GeometryConfigureFailure_RetriesBeforeAcceptingANewerFrame() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var lens = host.Lens;
        var source = new ScreenRegion(20, 30, 80, 60);
        PrepareOffscreenWindow(lens);
        lens.Show();
        await Dispatcher.Yield(DispatcherPriority.Loaded);
        await lens.SetSourceAsync(source, (nint)1);
        lens.UpdateCapture(CreateFrame(source, 47), true);
        await WaitUntilAsync(() => !PrivateAccess.Get<bool>(lens, "_geometryPending"),
            "초기 렌즈 viewport를 구성하지 못했다.");

        host.Relay.ConfigureFailure = new InvalidOperationException("일시 relay 구성 실패");
        lens.SetZoom(2.5);
        await WaitUntilAsync(() => host.Relay.Calls.Count(call => call == "configure") >= 2,
            "relay 구성 실패를 관찰하지 못했다.");
        Assert.IsNotNull(PrivateAccess.Get<DispatcherTimer>(lens, "_geometryRetryTimer"),
            "relay 구성 실패 뒤 geometry 재시도가 예약되지 않았다.");
        Assert.IsTrue(PrivateAccess.Get<bool>(lens, "_geometryPending"));

        host.Relay.ConfigureFailure = null;
        await WaitUntilAsync(() => !PrivateAccess.Get<bool>(lens, "_geometryPending"),
            "relay 구성 복구 뒤 geometry 대기가 해제되지 않았다.");
        var framesBeforeRecovery = host.Relay.FrameCount;
        lens.UpdateCapture(CreateFrame(source, 93), true);

        Assert.AreEqual(framesBeforeRecovery + 1, host.Relay.FrameCount,
            "geometry 재시도 완료 뒤 새 캡처만 freshness를 갱신해야 한다.");
    });

    private static void PrepareOffscreenWindow(SelectionPreviewWindow lens)
    {
        lens.ShowInTaskbar = false;
        lens.ShowActivated = false;
        lens.Left = -32000;
        lens.Top = -32000;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string failureMessage)
    {
        for (var attempt = 0; attempt < 80; ++attempt)
        {
            if (condition()) return;
            await Task.Delay(25);
        }
        Assert.Fail(failureMessage);
    }

    private static CapturedFrame CreateFrame(ScreenRegion region, byte value)
    {
        var pixels = new byte[region.Width * region.Height * 4];
        Array.Fill(pixels, value);
        return new CapturedFrame(region, pixels);
    }

    private static void ArrangeImage(Image image)
    {
        var size = new Size(image.Width, image.Height);
        image.Measure(size);
        image.Arrange(new Rect(new Point(), size));
    }

    private static int ReadFirstPixel(Image image)
    {
        var bitmap = (WriteableBitmap)image.Source;
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        return bytes[0];
    }
}
