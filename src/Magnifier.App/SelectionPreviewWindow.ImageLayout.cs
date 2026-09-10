using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private readonly SemaphoreSlim _configurationLock = new(1, 1);
    private LensViewport? _configuredViewport;
    private nint _configuredFrameHandle;
    private bool _geometryQueued, _sizing;
    private double _requestedZoom = 2;

    public void SetZoom(double zoom)
    {
        if (!_editingAllowed || !double.IsFinite(zoom)) return;
        _requestedZoom = Math.Clamp(zoom, 0.25, 8);
        ResizeLens();
        QueueGeometryUpdate();
    }

    private async void ZoomDecrease_OnClick(object sender, RoutedEventArgs e) => await ChangeZoomAsync(Math.Max(0.25, Zoom - 0.5));
    private async void ZoomIncrease_OnClick(object sender, RoutedEventArgs e) => await ChangeZoomAsync(Zoom + 0.5);

    private async Task ChangeZoomAsync(double zoom)
    {
        if (!_editingAllowed) return;
        try
        {
            await PauseAsync("배율 변경 · 조작 자동 재개 대기");
            SetZoom(zoom);
            await ConfigureGeometryAsync();
        }
        catch (Exception exception) { PublishInputStatus($"배율 변경 실패: {exception.Message}"); }
    }

    private void ResizeLens()
    {
        if (_sizing || _currentRegion is not ScreenRegion region || !IsInitialized) return;
        _sizing = true;
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var work = WindowHandle != 0 ? _windows.GetWindowWorkArea(WindowHandle) : _windows.DesktopBounds;
            var availableWidth = work.Width;
            var availableHeight = work.Height;
            if (WindowHandle != 0 && IsLoaded)
            {
                var position = _windows.GetWindowBounds(WindowHandle);
                availableWidth = Math.Min(work.Width, Math.Max(1, work.X + work.Width - position.X));
                availableHeight = Math.Min(work.Height, Math.Max(1, work.Y + work.Height - position.Y));
            }
            var maxWidth = Math.Max(160, availableWidth / dpi.DpiScaleX);
            var maxHeight = Math.Max(180, availableHeight / dpi.DpiScaleY);
            var proposedWidth = Math.Min(maxWidth, Math.Max(560, region.Width * _requestedZoom / dpi.DpiScaleX + 18));
            Toolbar.Measure(new Size(Math.Max(1, proposedWidth - 4), double.PositiveInfinity));
            StatusPanel.Measure(new Size(Math.Max(1, proposedWidth - 4), double.PositiveInfinity));
            AuxiliaryTools.Measure(new Size(Math.Max(1, proposedWidth - 4), double.PositiveInfinity));
            var chromeHeight = 44 + Toolbar.DesiredSize.Height + StatusPanel.DesiredSize.Height + AuxiliaryTools.DesiredSize.Height;
            var fit = Math.Min((maxWidth - 18) * dpi.DpiScaleX / region.Width,
                Math.Max(1, maxHeight - chromeHeight - 2) * dpi.DpiScaleY / region.Height);
            Zoom = Math.Max(0.01, Math.Min(_requestedZoom, fit));
            Width = Math.Min(maxWidth, Math.Max(560, region.Width * Zoom / dpi.DpiScaleX + 18));
            Height = Math.Min(maxHeight, Math.Max(chromeHeight + 3, region.Height * Zoom / dpi.DpiScaleY + chromeHeight + 2));
            ZoomText.Text = $"{Zoom:0.##}×";
            ZoomText.ToolTip = Zoom + 0.01 < _requestedZoom
                ? $"요청 {_requestedZoom:0.##}× · 현재 모니터에 전체 원본이 보이도록 {Zoom:0.##}×로 맞췄습니다"
                : "원본 물리 픽셀 대비 표시 배율";
            UpdateLayout();
            UpdatePointMarkers();
            // Source changes can resize the content but never move this independent window.
        }
        finally { _sizing = false; }
    }

    private void QueueGeometryUpdate()
    {
        if (_geometryQueued || _closed || !IsLoaded || _sizing) return;
        _geometryQueued = true;
        Dispatcher.BeginInvoke(async () =>
        {
            _geometryQueued = false;
            if (_closed || !IsVisible) return;
            try
            {
                if (!_isMoving && _editingAllowed) ResizeLens();
                await ConfigureGeometryAsync();
                PlacementChanged?.Invoke();
            }
            catch (Exception exception) { PublishInputStatus($"렌즈 배치 설정 실패: {exception.Message}"); }
        }, DispatcherPriority.Loaded);
    }

    private void KeepLensOnScreen()
    {
        // Only an explicit lens move changes its position. Source edits never call this.
        if (WindowHandle == 0 || !_editingAllowed) return;
        var work = _windows.GetWindowWorkArea(WindowHandle);
        var bounds = _windows.GetWindowBounds(WindowHandle);
        var width = Math.Min(bounds.Width, work.Width);
        var height = Math.Min(bounds.Height, work.Height);
        var left = Math.Clamp(bounds.X, work.X, work.X + work.Width - width);
        var top = Math.Clamp(bounds.Y, work.Y, work.Y + work.Height - height);
        _windows.PlaceWindow(WindowHandle, new ScreenRegion(left, top, width, height));
    }

    private async Task ConfigureGeometryAsync()
    {
        if (_closed || !IsLoaded || WindowHandle == 0 || _frameHandle == 0) return;
        await _configurationLock.WaitAsync();
        try
        {
            if (_closed || _currentRegion is not ScreenRegion region ||
                !TryGetRenderedImageBounds(region, out var bounds)) return;
            var start = CapturedImage.PointToScreen(bounds.TopLeft);
            var end = CapturedImage.PointToScreen(bounds.BottomRight);
            var left = (int)Math.Round(start.X);
            var top = (int)Math.Round(start.Y);
            var width = (int)Math.Round(end.X) - left;
            var height = (int)Math.Round(end.Y) - top;
            if (width <= 0 || height <= 0) return;
            var viewport = new LensViewport(region, new ScreenRegion(left, top, width, height));
            if (_configuredViewport == viewport && _configuredFrameHandle == _frameHandle) return;
            await _relay.ConfigureAsync(viewport, WindowHandle, _frameHandle);
            _configuredViewport = viewport;
            _configuredFrameHandle = _frameHandle;
            // Show the scale after actual layout, including possible letterboxing.
            Zoom = (double)width / region.Width;
            ZoomText.Text = $"{Zoom:0.##}×";
        }
        finally { _configurationLock.Release(); }
    }

    protected override async void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (!IsLoaded || _closed) return;
        try
        {
            await PauseAsync("화면 DPI 변경 · 조작 자동 재개 대기");
            ResizeLens();
            QueueGeometryUpdate();
        }
        catch { }
    }

    private bool TryGetRenderedImageBounds(ScreenRegion region, out Rect renderedBounds)
    {
        if (CapturedImage.ActualWidth <= 0 || CapturedImage.ActualHeight <= 0)
        {
            renderedBounds = default;
            return false;
        }
        var scale = Math.Min(CapturedImage.ActualWidth / region.Width, CapturedImage.ActualHeight / region.Height);
        var width = region.Width * scale;
        var height = region.Height * scale;
        renderedBounds = new Rect((CapturedImage.ActualWidth - width) / 2,
            (CapturedImage.ActualHeight - height) / 2, width, height);
        return true;
    }

    private bool TryMapToScreen(Point point, out ScreenPoint screenPoint, out PreviewPoint visualPoint)
    {
        screenPoint = default;
        visualPoint = default;
        if (_currentRegion is not ScreenRegion region || !TryGetRenderedImageBounds(region, out var bounds)) return false;
        if (point.X < bounds.Left || point.Y < bounds.Top || point.X >= bounds.Right || point.Y >= bounds.Bottom) return false;
        var local = new PreviewPoint(point.X - bounds.X, point.Y - bounds.Y);
        visualPoint = new PreviewPoint(local.X / bounds.Width, local.Y / bounds.Height);
        screenPoint = PreviewCoordinateMapper.MapToScreen(region, new PreviewSize(bounds.Width, bounds.Height), local);
        return true;
    }
}
