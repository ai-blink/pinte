using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Magnifier.Core;

namespace Magnifier.App;

/// <summary>숨긴 렌즈를 마우스로 다시 펼치는, 캡처에서 제외된 작은 오버레이다.</summary>
public partial class LensCollapseOverlayWindow : Window
{
    private readonly IScreenCapture _capture;
    private readonly IWindowEnvironment _windows;
    private nint _windowHandle;
    private bool _captureExcluded;
    private bool _closed;
    private bool _hideFromScreenCapture;
    private Point? _dragStartPointer;
    private ScreenRegion? _dragStartBounds;
    private bool _draggingIcon;
    private bool _suppressExpandOnce;
    private const int DragThresholdPixels = 4;

    public LensCollapseOverlayWindow(IScreenCapture capture, IWindowEnvironment windows, bool hideFromScreenCapture = false)
    {
        InitializeComponent();
        _capture = capture;
        _windows = windows;
        _hideFromScreenCapture = hideFromScreenCapture;
    }

    public event Action? ExpandRequested;
    public string? FailureReason { get; private set; }

    public void SetCaptureExclusion(bool excludeFromCapture)
    {
        _hideFromScreenCapture = excludeFromCapture;
        if (_windowHandle == nint.Zero) return;
        _capture.SetWindowCaptureExclusion(_windowHandle, excludeFromCapture);
        _captureExcluded = excludeFromCapture;
    }

    public bool ShowAt(ScreenPoint anchor)
    {
        if (_closed) return false;
        FailureReason = null;

        try
        {
            Hide();
            _windowHandle = new WindowInteropHelper(this).EnsureHandle();
            SetCaptureExclusion(_hideFromScreenCapture);
            var bounds = new ScreenRegion(anchor.X - 26, anchor.Y - 26, 52, 52);
            _windows.PlaceWindow(_windowHandle, bounds);
            Show();
            // 첫 Show 및 DPI 전환 뒤에도 물리 좌표와 최상위 Z-order를 다시 맞춘다.
            _windows.PlaceTopmostWindow(_windowHandle, bounds);
            return true;
        }
        catch (Exception exception)
        {
            Hide();
            FailureReason = $"렌즈 펼치기 아이콘을 준비하지 못했습니다: {exception.Message}";
            return false;
        }
    }

    private void ExpandButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _windowHandle == nint.Zero) return;

        try
        {
            _dragStartBounds = _windows.GetWindowBounds(_windowHandle);
            _dragStartPointer = PointToScreen(e.GetPosition(this));
            _draggingIcon = false;
        }
        catch
        {
            EndIconDrag(suppressExpand: false, releaseMouseCapture: true);
        }
    }

    private void ExpandButton_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartPointer is not Point startPointer || _dragStartBounds is not ScreenRegion startBounds) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndIconDrag(suppressExpand: false, releaseMouseCapture: true);
            return;
        }

        var pointer = PointToScreen(e.GetPosition(this));
        var horizontalPixels = (int)Math.Round(pointer.X - startPointer.X);
        var verticalPixels = (int)Math.Round(pointer.Y - startPointer.Y);
        if (!_draggingIcon && Math.Abs(horizontalPixels) < DragThresholdPixels && Math.Abs(verticalPixels) < DragThresholdPixels)
            return;

        try
        {
            _draggingIcon = true;
            PlaceDraggedIcon(startBounds, horizontalPixels, verticalPixels);
            e.Handled = true;
        }
        catch
        {
            EndIconDrag(suppressExpand: true, releaseMouseCapture: true);
            e.Handled = true;
        }
    }

    private void ExpandButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var suppressExpand = _draggingIcon;
        // 일반 클릭은 ButtonBase가 MouseUp에서 Click을 올리고 캡처를 해제해야 한다.
        // 드래그만 Preview 단계에서 소비하므로 여기서 수동으로 캡처를 끝낸다.
        EndIconDrag(suppressExpand, releaseMouseCapture: suppressExpand);
        if (suppressExpand) e.Handled = true;
    }

    private void ExpandButton_OnLostMouseCapture(object sender, MouseEventArgs e) =>
        EndIconDrag(suppressExpand: false, releaseMouseCapture: false);

    // 화면 좌표는 DIP가 아니라 물리 픽셀로 유지해 혼합 DPI에서도 아이콘 크기를 바꾸지 않는다.
    internal void PlaceDraggedIcon(ScreenRegion startBounds, int horizontalPixels, int verticalPixels)
    {
        if (_windowHandle == nint.Zero) return;
        _windows.PlaceTopmostWindow(_windowHandle, new ScreenRegion(
            startBounds.X + horizontalPixels,
            startBounds.Y + verticalPixels,
            startBounds.Width,
            startBounds.Height));
    }

    private void EndIconDrag(bool suppressExpand, bool releaseMouseCapture)
    {
        _dragStartPointer = null;
        _dragStartBounds = null;
        _draggingIcon = false;
        if (suppressExpand)
        {
            // Button이 MouseUp 뒤 Click을 올리는 경우에만 해당 Click을 막고, 다음 클릭은 살린다.
            _suppressExpandOnce = true;
            _ = Dispatcher.BeginInvoke(() => _suppressExpandOnce = false);
        }
        if (releaseMouseCapture && Mouse.Captured == ExpandButton) ExpandButton.ReleaseMouseCapture();
    }

    private void ExpandButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_suppressExpandOnce)
        {
            _suppressExpandOnce = false;
            e.Handled = true;
            return;
        }
        ExpandRequested?.Invoke();
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        try
        {
            if (_captureExcluded) _capture.SetWindowCaptureExclusion(_windowHandle, false);
        }
        finally
        {
            _captureExcluded = false;
            base.OnClosed(e);
        }
    }
}
