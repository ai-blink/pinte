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
        RequestViewportCentering();
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
            // 배율은 렌즈 내부의 이미지에만 적용한다. 바깥 창은 독립 viewport라서
            // 배율·원본 변경으로 Width/Height를 수정하지 않는다.
            Zoom = _requestedZoom;
            CapturedImage.Width = Math.Max(1, region.Width * Zoom / dpi.DpiScaleX);
            CapturedImage.Height = Math.Max(1, region.Height * Zoom / dpi.DpiScaleY);
            ZoomText.Text = $"{Zoom:0.##}×";
            ZoomText.ToolTip = "원본 물리 픽셀 대비 표시 배율 · 렌즈 창 크기는 유지합니다";
            UpdatePanSurface(CapturedImage.Width, CapturedImage.Height);
            UpdatePointMarkers();
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
                !TryGetVisibleViewport(region, out var viewport)) return;
            if (_configuredViewport == viewport && _configuredFrameHandle == _frameHandle) return;
            await _relay.ConfigureAsync(viewport, WindowHandle, _frameHandle);
            _configuredViewport = viewport;
            _configuredFrameHandle = _frameHandle;
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
        if (!TryGetScreenBounds(CapturedImage, out var image) ||
            !TryGetScreenBounds(ImageViewport, out var canvas)) return false;

        var left = Math.Max(image.X, canvas.X);
        var top = Math.Max(image.Y, canvas.Y);
        var right = Math.Min(image.X + image.Width, canvas.X + canvas.Width);
        var bottom = Math.Min(image.Y + image.Height, canvas.Y + canvas.Height);
        if (right <= left || bottom <= top) return false;

        var destination = new ScreenRegion(left, top, right - left, bottom - top);
        var sourceLeft = source.X + ScaleOffset(destination.X - image.X, image.Width, source.Width, roundUp: false);
        var sourceTop = source.Y + ScaleOffset(destination.Y - image.Y, image.Height, source.Height, roundUp: false);
        var sourceRight = source.X + ScaleOffset(destination.X + destination.Width - image.X,
            image.Width, source.Width, roundUp: true);
        var sourceBottom = source.Y + ScaleOffset(destination.Y + destination.Height - image.Y,
            image.Height, source.Height, roundUp: true);
        var sourceCrop = new ScreenRegion(sourceLeft, sourceTop,
            Math.Max(1, sourceRight - sourceLeft), Math.Max(1, sourceBottom - sourceTop));
        viewport = new LensViewport(sourceCrop, destination);
        return true;
    }

    private static int ScaleOffset(int value, int inputLength, int outputLength, bool roundUp)
    {
        var scaled = Math.Clamp((double)value / inputLength * outputLength, 0, outputLength);
        var offset = roundUp ? (int)Math.Ceiling(scaled) : (int)Math.Floor(scaled);
        return Math.Clamp(offset, roundUp ? 1 : 0, outputLength);
    }

    private static bool TryGetScreenBounds(FrameworkElement element, out ScreenRegion bounds)
    {
        bounds = default;
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
        var start = element.PointToScreen(new Point(0, 0));
        var end = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        var left = (int)Math.Floor(Math.Min(start.X, end.X));
        var top = (int)Math.Floor(Math.Min(start.Y, end.Y));
        var right = (int)Math.Ceiling(Math.Max(start.X, end.X));
        var bottom = (int)Math.Ceiling(Math.Max(start.Y, end.Y));
        if (right <= left || bottom <= top) return false;
        bounds = new ScreenRegion(left, top, right - left, bottom - top);
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
