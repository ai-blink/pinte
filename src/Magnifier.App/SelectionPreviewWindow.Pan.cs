using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Magnifier.App.Localization;

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
        catch (Exception exception) { PublishInputStatus(string.Format(Loc.Instance["Lens_Status_PanModeChangeFailed_Format"], exception.Message)); }
        e.Handled = true;
    }

    private async Task SetPanModeAsync(bool enabled)
    {
        if (_handToolEnabled == enabled) return;
        if (!enabled)
        {
            EndPan();
            await RefreshGeometryAsync();
        }
        _handToolEnabled = enabled;
        await ApplyInputSuspensionAsync(enabled
            ? Loc.Instance["Lens_Reason_PanOn"] : Loc.Instance["Lens_Reason_PanOff"]);
        ApplyPanModeUi();
        UpdateControls();
    }

    public Task EndPanModeAsync() => SetPanModeAsync(false);

    private void ApplyPanModeUi()
    {
        if (!IsInitialized) return;
        // 켜짐·꺼짐은 강조 색으로 구분해 일반 툴바 폭을 줄인다.
        PanModeButton.Content = _normalToolbarNarrow ? "✋" : Loc.Instance["Lens_PanMode_Full"];
        PanModeButton.ToolTip = _handToolEnabled
            ? Loc.Instance["Lens_PanMode_Tooltip_On"]
            : Loc.Instance["Lens_PanMode_Tooltip_Off"];
        PanModeButton.Style = _handToolEnabled
            ? (Style)FindResource("LensAccentButtonStyle")
            : (Style)FindResource("LensSoftAccentButtonStyle");
        System.Windows.Automation.AutomationProperties.SetName(PanModeButton,
            _handToolEnabled ? Loc.Instance["Lens_PanMode_Automation_On"] : Loc.Instance["Lens_PanMode_Automation_Off"]);
        CompactPanModeButton.Content = _handToolEnabled ? "✋✓" : "✋○";
        CompactPanModeButton.ToolTip = PanModeButton.ToolTip;
        System.Windows.Automation.AutomationProperties.SetName(CompactPanModeButton,
            _handToolEnabled ? Loc.Instance["Lens_PanMode_Automation_On"] : Loc.Instance["Lens_PanMode_Automation_Off"]);
        CompactPanModeButton.Style = _handToolEnabled
            ? (Style)FindResource("CompactActiveButtonStyle")
            : (Style)FindResource("CompactButtonStyle");
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
            if (_displayMode == LensDisplayMode.Compact)
                HorizontalPanBar.Visibility = VerticalPanBar.Visibility = Visibility.Collapsed;
            PanCorner.Visibility = HorizontalPanBar.Visibility == Visibility.Visible && VerticalPanBar.Visibility == Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;
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
