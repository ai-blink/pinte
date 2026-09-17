using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private const double MinimumZoom = 0.25;
    private const double MaximumZoom = 8;
    private const double FastZoomStep = 0.5;
    private readonly SemaphoreSlim _configurationLock = new(1, 1);
    private LensViewport? _configuredViewport;
    private nint _configuredFrameHandle;
    private DispatcherTimer? _geometryRetryTimer;
    private bool _geometryQueued, _sizing, _geometryPending, _syncingFineZoomControls,
        _fineZoomSliderDragging, _fineZoomTextCommitInProgress;
    private int _geometryRevision;
    private double _requestedZoom = 2;

    public void SetZoom(double zoom)
    {
        if (!_editingAllowed || !double.IsFinite(zoom)) return;
        _requestedZoom = NormalizeZoom(zoom);
        RequestViewportCentering();
        ResizeLens();
        QueueGeometryUpdate();
    }

    private async void ZoomDecrease_OnClick(object sender, RoutedEventArgs e) => await ChangeZoomAsync(Zoom - FastZoomStep);
    private async void ZoomIncrease_OnClick(object sender, RoutedEventArgs e) => await ChangeZoomAsync(Zoom + FastZoomStep);

    private async Task ChangeZoomAsync(double zoom)
    {
        if (!_editingAllowed) return;
        zoom = NormalizeZoom(zoom);
        if (Math.Abs(Zoom - zoom) < 0.001)
        {
            SynchronizeFineZoomControls();
            return;
        }
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
            // 배율은 렌즈 내부의 이미지에만 적용한다. 바깥 창은 독립 viewport라서
            // 배율·원본 변경으로 Width/Height를 수정하지 않는다.
            Zoom = _requestedZoom;
            CapturedImage.Width = Math.Max(1, region.Width * Zoom / dpi.DpiScaleX);
            CapturedImage.Height = Math.Max(1, region.Height * Zoom / dpi.DpiScaleY);
            ZoomText.Text = $"{Zoom:0.##}×";
            CompactZoomText.Text = ZoomText.Text;
            ZoomText.ToolTip = "원본 물리 픽셀 대비 표시 배율 · 렌즈 창 크기는 유지합니다";
            SynchronizeFineZoomControls();
            UpdatePanSurface(CapturedImage.Width, CapturedImage.Height);
            UpdatePointMarkers();
        }
        finally { _sizing = false; }
    }

    private static double NormalizeZoom(double zoom)
    {
        var clamped = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        if (clamped < 0.3) return MinimumZoom;
        return Math.Clamp(Math.Round(clamped, 1, MidpointRounding.AwayFromZero), MinimumZoom, MaximumZoom);
    }

    private void SynchronizeFineZoomControls()
    {
        if (!IsInitialized) return;
        _syncingFineZoomControls = true;
        try
        {
            FineZoomSlider.Value = Zoom;
            FineZoomText.Text = Zoom.ToString("0.##", CultureInfo.CurrentCulture);
        }
        finally { _syncingFineZoomControls = false; }
    }

    private void FineZoomSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingFineZoomControls || _fineZoomSliderDragging || !_editingAllowed || _currentRegion is null) return;
        _ = ChangeZoomAsync(e.NewValue);
    }

    private void FineZoomSlider_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _fineZoomSliderDragging = _editingAllowed && _currentRegion is not null;

    private async void FineZoomSlider_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_fineZoomSliderDragging) return;
        _fineZoomSliderDragging = false;
        await ChangeZoomAsync(FineZoomSlider.Value);
    }

    private async void FineZoomText_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await CommitFineZoomTextAsync();
    }

    private async void FineZoomText_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        await CommitFineZoomTextAsync();

    private async Task CommitFineZoomTextAsync()
    {
        if (_syncingFineZoomControls || _fineZoomTextCommitInProgress || !_editingAllowed || _currentRegion is null) return;
        _fineZoomTextCommitInProgress = true;
        try
        {
            if (!TryParseFineZoom(FineZoomText.Text, out var zoom))
            {
                SynchronizeFineZoomControls();
                PublishInputStatus("배율은 0.25에서 8 사이의 숫자로 입력하세요");
                return;
            }
            await ChangeZoomAsync(zoom);
        }
        finally { _fineZoomTextCommitInProgress = false; }
    }

    private static bool TryParseFineZoom(string text, out double zoom)
    {
        var candidate = text.Trim().TrimEnd('×').Trim();
        var parsed = double.TryParse(candidate, NumberStyles.Float, CultureInfo.CurrentCulture, out zoom)
            || double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out zoom);
        return parsed && double.IsFinite(zoom) && zoom is >= MinimumZoom and <= MaximumZoom;
    }

    private void QueueGeometryUpdate()
    {
        if (_closed || !IsLoaded || _sizing) return;
        InvalidateGeometry();
        if (_geometryQueued) return;
        _geometryQueued = true;
        var pause = PauseAsync("렌즈 배치 변경 · 최신 화면 확인 대기");
        Dispatcher.BeginInvoke(async () =>
        {
            _geometryQueued = false;
            try
            {
                await pause;
                if (_closed || !IsVisible) return;
                if (!_isMoving && _editingAllowed) ResizeLens();
                await ConfigureGeometryAsync();
                PlacementChanged?.Invoke();
            }
            catch (Exception exception) { PublishInputStatus($"렌즈 배치 설정 실패: {exception.Message}"); }
        }, DispatcherPriority.Loaded);
    }

    private void InvalidateGeometry()
    {
        if (_closed || !IsLoaded) return;
        _geometryPending = true;
        ++_geometryRevision;
        GeometryInvalidated?.Invoke();
    }

    public async Task RefreshGeometryAsync()
    {
        if (_closed || !IsLoaded) return;
        InvalidateGeometry();
        await PauseAsync("렌즈 좌표 갱신 · 최신 화면 확인 대기");
        ResizeLens();
        UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Loaded);
        await ConfigureGeometryAsync();
        PlacementChanged?.Invoke();
    }

    private void ImageViewport_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePanBars();
        UpdatePointMarkers();
        QueueGeometryUpdate();
    }

    private async Task<bool> ConfigureGeometryAsync()
    {
        if (_closed) return false;
        if (!IsLoaded || WindowHandle == 0 || _frameHandle == 0)
        {
            ScheduleGeometryRetry();
            return false;
        }
        await _configurationLock.WaitAsync();
        try
        {
            if (_closed || _currentRegion is not ScreenRegion region ||
                !TryGetVisibleViewport(region, out var viewport))
            {
                ScheduleGeometryRetry();
                return false;
            }
            var revision = _geometryRevision;
            if (_configuredViewport != viewport || _configuredFrameHandle != _frameHandle)
            {
                GeometryInvalidated?.Invoke();
                await _relay.ConfigureAsync(viewport, WindowHandle, _frameHandle);
            }
            _configuredViewport = viewport;
            _configuredFrameHandle = _frameHandle;
            if (revision != _geometryRevision)
            {
                ScheduleGeometryRetry();
                return false;
            }
            _geometryPending = false;
            return true;
        }
        catch
        {
            // A transient relay/viewport failure must not leave capture frames visible
            // while geometryPending blocks their freshness signal forever.
            ScheduleGeometryRetry();
            throw;
        }
        finally { _configurationLock.Release(); }
    }

    private void ScheduleGeometryRetry()
    {
        if (_closed || !IsVisible || !_geometryPending) return;
        _geometryRetryTimer ??= CreateGeometryRetryTimer();
        if (!_geometryRetryTimer.IsEnabled) _geometryRetryTimer.Start();
    }

    private DispatcherTimer CreateGeometryRetryTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += GeometryRetryTimer_OnTick;
        return timer;
    }

    private async void GeometryRetryTimer_OnTick(object? sender, EventArgs e)
    {
        _geometryRetryTimer?.Stop();
        if (_closed || !IsVisible || !_geometryPending) return;
        try
        {
            if (await ConfigureGeometryAsync()) PlacementChanged?.Invoke();
        }
        catch (Exception exception) { PublishInputStatus($"렌즈 좌표 재시도 실패: {exception.Message}"); }
    }

    private void StopGeometryRetry() => _geometryRetryTimer?.Stop();

    protected override async void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (!IsLoaded || _closed) return;
        InvalidateGeometry();
        try
        {
            await PauseAsync("화면 DPI 변경 · 조작 자동 재개 대기");
            RequestViewportCentering();
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
        // Explicit dimensions keep the full captured bitmap at the requested scale.
        // ImageBorder clips any overflowing portion; direct input uses the matching
        // visible source crop from TryGetVisibleViewport.
        renderedBounds = new Rect(0, 0, CapturedImage.ActualWidth, CapturedImage.ActualHeight);
        return true;
    }

    private bool TryGetVisibleViewport(ScreenRegion source, out LensViewport viewport)
    {
        viewport = default!;
        if (!TryGetPhysicalBounds(CapturedImage, out var imageOrigin, out var imageSize) ||
            !TryGetPhysicalBounds(ImageViewport, out var canvasOrigin, out var canvasSize)) return false;
        return LensViewport.TryCreate(source, imageOrigin, imageSize, canvasOrigin, canvasSize, out viewport);
    }

    private static bool TryGetPhysicalBounds(FrameworkElement element, out PreviewPoint origin, out PreviewSize size)
    {
        origin = default;
        size = default;
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
        var start = element.PointToScreen(new Point(0, 0));
        var end = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        origin = new PreviewPoint(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
        size = new PreviewSize(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        return size.IsValid;
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
