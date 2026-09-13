using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
