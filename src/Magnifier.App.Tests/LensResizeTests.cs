using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class LensResizeTests
{
    [TestMethod]
    public Task ResizeControls_KeepTheLensContentAreaUsableAtMinimumSize() => StaTest.Run(() =>
    {
        using var host = new HiddenWindows();
        var lens = host.Lens;
        var outerBorder = (Border)lens.FindName("OuterBorder");

        lens.SetResizeControlsVisible(true);

        Assert.AreEqual(new Thickness(24), outerBorder.Margin);
        Assert.AreEqual(688d, lens.MinWidth);
        Assert.AreEqual(408d, lens.MinHeight);

        lens.SetResizeControlsVisible(false);

        Assert.AreEqual(new Thickness(8), outerBorder.Margin);
        Assert.AreEqual(640d, lens.MinWidth);
        Assert.AreEqual(360d, lens.MinHeight);
        var resizeLayer = lens.FindName("ResizeLayer") as Grid;
        Assert.IsNotNull(resizeLayer);
        var controls = resizeLayer.Children.OfType<Thumb>().ToArray();
        Assert.AreEqual(8, controls.Length);
        Assert.AreEqual(24d, controls.Single(thumb => (string)thumb.Tag == "NW").Width);
        Assert.AreEqual(48d, controls.Single(thumb => (string)thumb.Tag == "SW").Width);
        Assert.AreEqual(20d, controls.Single(thumb => (string)thumb.Tag == "N").Height);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task CompactToolbar_ContainsTheLensHideToggleAtMinimumWidth() => StaTest.Run(() =>
    {
        using var host = new HiddenWindows();
        var lens = host.Lens;

        lens.SetDisplayMode(LensDisplayMode.Compact);

        var hide = (Button)lens.FindName("CompactHideLensButton");
        var returnButton = (Button)lens.FindName("CompactReturnButton");
        Assert.AreEqual("숨김", hide.Content);
        Assert.AreEqual(44d, returnButton.MinWidth);
        Assert.IsTrue(lens.MinWidth >= 440d);
        return Task.CompletedTask;
    });

    [DataTestMethod]
    [DataRow(LensDisplayMode.Normal, ToolbarPlacement.Top, 1)]
    [DataRow(LensDisplayMode.Normal, ToolbarPlacement.Bottom, 4)]
    [DataRow(LensDisplayMode.Compact, ToolbarPlacement.Top, 1)]
    [DataRow(LensDisplayMode.Compact, ToolbarPlacement.Bottom, 4)]
    public Task SharedToolbarPlacement_PreservesSourceZoomAndOuterRect(
        LensDisplayMode mode, ToolbarPlacement placement, int row) => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var lens = host.Lens;
        var source = new ScreenRegion(-900, -200, 321, 181);
        await lens.SetSourceAsync(source, 0);
        lens.SetZoom(2.5);
        lens.Left = -700;
        lens.Top = 30;
        lens.Width = 800;
        lens.Height = 640;
        var toolbar = (FrameworkElement)lens.FindName("Toolbar");

        lens.SetDisplayMode(mode);
        lens.SetToolbarPlacement(placement);

        Assert.AreSame(toolbar, lens.FindName("Toolbar"));
        Assert.AreEqual(row, Grid.GetRow(toolbar));
        Assert.AreEqual(source, lens.CurrentRegion);
        Assert.AreEqual(2.5, lens.Zoom);
        Assert.AreEqual(-700d, lens.Left);
        Assert.AreEqual(30d, lens.Top);
        Assert.AreEqual(800d, lens.Width);
        Assert.AreEqual(640d, lens.Height);
        Assert.AreEqual((nint)0, lens.WindowHandle);
        Assert.AreEqual(0, host.Pointer.CallCount);
    });

    [TestMethod]
    public Task FineZoomControls_NormalizeTenthsAndCommitDirectInput() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var lens = host.Lens;
        await lens.SetSourceAsync(new ScreenRegion(20, 30, 80, 60), 0);

        lens.SetZoom(2.26);

        var slider = (Slider)lens.FindName("FineZoomSlider");
        var text = (TextBox)lens.FindName("FineZoomText");
        Assert.AreEqual(2.3, lens.Zoom, 0.001);
        Assert.AreEqual(2.3, slider.Value, 0.001);
        Assert.AreEqual("2.3", text.Text);

        text.Text = "3.4×";
        await PrivateAccess.CallAsync(lens, "CommitFineZoomTextAsync");

        Assert.AreEqual(3.4, lens.Zoom, 0.001);
        Assert.AreEqual(3.4, slider.Value, 0.001);
        Assert.AreEqual("3.4", text.Text);
        CollectionAssert.Contains(host.Relay.Calls, "pause");
    });

    [TestMethod]
    public Task CompactZoomText_AcceptsManualTenths() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var lens = host.Lens;
        await lens.SetSourceAsync(new ScreenRegion(20, 30, 80, 60), 0);
        lens.SetDisplayMode(LensDisplayMode.Compact);
        var text = (TextBox)lens.FindName("CompactZoomText");

        text.Text = "1.7";
        await PrivateAccess.CallAsync(lens, "CommitZoomTextAsync", text);

        Assert.AreEqual(1.7, lens.Zoom, 0.001);
        Assert.AreEqual("1.7", text.Text);
        Assert.IsTrue(text.IsEnabled);
    });

    [TestMethod]
    public Task ExternalResume_WhileHandToolActive_KeepsRelaySuspended() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        await host.Lens.SetInputSuspendedAsync(true, "모달 시작");
        await PrivateAccess.CallAsync(host.Lens, "SetPanModeAsync", true);

        await host.Lens.SetInputSuspendedAsync(false, "모달 종료");

        Assert.IsTrue(host.Relay.Suspensions[^1]);
        await host.Lens.EndPanModeAsync();
        Assert.IsFalse(host.Relay.Suspensions[^1]);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.AreEqual(0, host.Pointer.CallCount);
    });

    [TestMethod]
    public Task HandToolExit_DuringSourceEditing_KeepsExternalSuspension() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        await PrivateAccess.CallAsync(host.Lens, "SetPanModeAsync", true);
        await host.Lens.SetInputSuspendedAsync(true, "영역 편집 시작");

        await host.Lens.EndPanModeAsync();

        Assert.IsTrue(host.Relay.Suspensions[^1]);
        await host.Lens.SetInputSuspendedAsync(false, "영역 편집 완료");
        Assert.IsFalse(host.Relay.Suspensions[^1]);
        Assert.AreEqual(0, host.Relay.StartCount);
    });

    [TestMethod]
    public Task ResizeEnd_DuringExternalSuspension_CannotResumeRelay() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        PrivateAccess.Set(host.Lens, "_isResizing", true);
        await PrivateAccess.CallAsync(host.Lens, "ApplyInputSuspensionAsync", "렌즈 크기 조절 시작");
        Assert.IsTrue(host.Relay.Suspensions[^1]);
        await host.Lens.SetInputSuspendedAsync(true, "영역 편집 보류");

        PrivateAccess.Set(host.Lens, "_isResizing", false);
        await PrivateAccess.CallAsync(host.Lens, "ApplyInputSuspensionAsync", "렌즈 크기 조절 종료");

        Assert.IsTrue(host.Relay.Suspensions[^1]);
        await host.Lens.SetInputSuspendedAsync(false, "영역 편집 종료");
        Assert.IsFalse(host.Relay.Suspensions[^1]);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.AreEqual(0, host.Pointer.CallCount);
    });

    [TestMethod]
    public Task ExternalResume_DuringResize_KeepsRelaySuspended() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        await host.Lens.SetInputSuspendedAsync(true, "외부 보류 시작");
        PrivateAccess.Set(host.Lens, "_isResizing", true);

        await host.Lens.SetInputSuspendedAsync(false, "외부 보류 종료");

        Assert.IsTrue(host.Relay.Suspensions[^1]);
        PrivateAccess.Set(host.Lens, "_isResizing", false);
        await PrivateAccess.CallAsync(host.Lens, "ApplyInputSuspensionAsync", "렌즈 크기 조절 종료");
        Assert.IsFalse(host.Relay.Suspensions[^1]);
        Assert.AreEqual(0, host.Relay.StartCount);
    });

    [TestMethod]
    public Task AfterStop_NewFrameAndHandToolExit_DoNotIssueAnotherStart() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var source = new ScreenRegion(10, 20, 80, 60);
        await host.Lens.SetSourceAsync(source, 0);
        await PrivateAccess.CallAsync(host.Lens, "SetPanModeAsync", true);
        PrivateAccess.Set(host.Lens, "_isResizing", true);
        PrivateAccess.Set(host.Lens, "_isMoving", true);
        await host.Lens.StopAsync("명시적 중지");

        await host.Lens.EndPanModeAsync();
        host.Lens.UpdateCapture(new CapturedFrame(source, new byte[80 * 60 * 4]), true);

        Assert.AreEqual(1, host.Relay.StopCount);
        Assert.AreEqual(1, host.Relay.FrameCount);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.IsFalse(PrivateAccess.Get<bool>(host.Lens, "_handToolEnabled"));
        Assert.IsFalse(PrivateAccess.Get<bool>(host.Lens, "_isResizing"));
        Assert.IsFalse(PrivateAccess.Get<bool>(host.Lens, "_isMoving"));
        Assert.IsFalse(host.Relay.Suspensions[^1], "내부 보류 해제는 새 입력 요청을 만들지 않는다.");
        Assert.AreEqual(0, host.Pointer.CallCount);
    });

    [TestMethod]
    public Task AfterInputFailure_NewFrameAndExternalResume_DoNotIssueAnotherStart() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var source = new ScreenRegion(10, 20, 80, 60);
        await host.Lens.SetSourceAsync(source, 0);
        PrivateAccess.Set(host.Lens, "_relayStatus",
            new RelayStatus(false, false, false, false, default, "입력 실패", InputRequested: false));
        await host.Lens.SetInputSuspendedAsync(true, "영역 편집");

        await host.Lens.SetInputSuspendedAsync(false, "편집 종료");
        host.Lens.UpdateCapture(new CapturedFrame(source, new byte[80 * 60 * 4]), true);

        Assert.AreEqual(1, host.Relay.FrameCount);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.AreEqual(0, host.Pointer.CallCount);
    });
}
