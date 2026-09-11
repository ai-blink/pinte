using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private bool _handToolEnabled, _isPanning, _syncingPanBars, _centerViewportOnLayout;
    private Point _panStart;
    private double _panHorizontalStart, _panVerticalStart;

    private void RequestViewportCentering() => _centerViewportOnLayout = true;

    private async void PanMode_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_editingAllowed || AuxiliaryTools.IsExpanded) return;
        try { await SetPanModeAsync(!_handToolEnabled); }
        catch (Exception exception) { PublishInputStatus($"손 도구 변경 실패: {exception.Message}"); }
        e.Handled = true;
    }

    private async Task SetPanModeAsync(bool enabled)
    {
        if (_handToolEnabled == enabled) return;
        await SetInputSuspendedAsync(enabled,
            enabled ? "손 도구 · 실제 입력 일시 중지" : "손 도구 종료 · 새 화면 확인 뒤 조작 자동 재개");
        _handToolEnabled = enabled;
        if (!enabled) EndPan();
        ApplyPanModeUi();
        UpdateControls();
    }

    private void ApplyPanModeUi()
    {
        if (!IsInitialized) return;
        PanModeButton.Content = _handToolEnabled ? "✋ 이동 중" : "✋ 이동";
        PanModeButton.ToolTip = _handToolEnabled
            ? "손 도구 켜짐: 렌즈 안을 끌어 확대된 위치를 이동합니다"
            : "손 도구: 렌즈 안을 끌어 확대된 위치를 이동합니다";
        PanModeButton.Style = _handToolEnabled
            ? (Style)FindResource("AccentButtonStyle")
            : (Style)FindResource("SoftAccentButtonStyle");
        ImageViewport.Cursor = _handToolEnabled ? Cursors.Hand : Cursors.Arrow;
    }

    private void ImageViewport_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_handToolEnabled || !_editingAllowed) return;
        _isPanning = true;
        _panStart = e.GetPosition(ImageViewport);
        _panHorizontalStart = ImageScroller.HorizontalOffset;
        _panVerticalStart = ImageScroller.VerticalOffset;
        ImageViewport.CaptureMouse();
        e.Handled = true;
    }

    private void ImageViewport_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var point = e.GetPosition(ImageViewport);
        ImageScroller.ScrollToHorizontalOffset(_panHorizontalStart - (point.X - _panStart.X));
        ImageScroller.ScrollToVerticalOffset(_panVerticalStart - (point.Y - _panStart.Y));
        e.Handled = true;
    }

    private void ImageViewport_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning) return;
        EndPan();
        e.Handled = true;
    }

    private void ImageViewport_OnLostMouseCapture(object sender, MouseEventArgs e) => EndPan();

    private void EndPan()
    {
        _isPanning = false;
        if (Mouse.Captured == ImageViewport) ImageViewport.ReleaseMouseCapture();
    }

    private void ImageScroller_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdatePanBars();
        if (e.HorizontalChange == 0 && e.VerticalChange == 0) return;
        UpdatePointMarkers();
        QueueGeometryUpdate();
    }

    private void HorizontalPanBar_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_syncingPanBars) ImageScroller.ScrollToHorizontalOffset(e.NewValue);
    }

    private void VerticalPanBar_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_syncingPanBars) ImageScroller.ScrollToVerticalOffset(e.NewValue);
    }

    private void UpdatePanSurface(double width, double height)
    {
        ZoomedContent.Width = width;
        ZoomedContent.Height = height;
        UpdateLayout();
        CenterViewportIfRequested();
        UpdatePanBars();
    }

    private void CenterViewportIfRequested()
    {
        if (!_centerViewportOnLayout || ImageScroller.ViewportWidth <= 0 || ImageScroller.ViewportHeight <= 0) return;
        ImageScroller.ScrollToHorizontalOffset(Math.Max(0, (ImageScroller.ExtentWidth - ImageScroller.ViewportWidth) / 2));
        ImageScroller.ScrollToVerticalOffset(Math.Max(0, (ImageScroller.ExtentHeight - ImageScroller.ViewportHeight) / 2));
        _centerViewportOnLayout = false;
    }

    private void UpdatePanBars()
    {
        if (!IsInitialized) return;
        _syncingPanBars = true;
        try
        {
            UpdatePanBar(HorizontalPanBar, ImageScroller.HorizontalOffset,
                ImageScroller.ExtentWidth, ImageScroller.ViewportWidth);
            UpdatePanBar(VerticalPanBar, ImageScroller.VerticalOffset,
                ImageScroller.ExtentHeight, ImageScroller.ViewportHeight);
        }
        finally { _syncingPanBars = false; }
    }

    private static void UpdatePanBar(ScrollBar bar, double offset, double extent, double viewport)
    {
        var maximum = Math.Max(0, extent - viewport);
        bar.Visibility = maximum > 0.5 ? Visibility.Visible : Visibility.Collapsed;
        bar.Maximum = maximum;
        bar.ViewportSize = Math.Max(0, viewport);
        bar.SmallChange = 32;
        bar.LargeChange = Math.Max(64, viewport * 0.75);
        bar.Value = Math.Clamp(offset, 0, maximum);
    }
}
