using System.Windows;
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

    public LensCollapseOverlayWindow(IScreenCapture capture, IWindowEnvironment windows)
    {
        InitializeComponent();
        _capture = capture;
        _windows = windows;
    }

    public event Action? ExpandRequested;
    public string? FailureReason { get; private set; }

    public bool ShowAt(ScreenPoint anchor)
    {
        if (_closed) return false;
        FailureReason = null;

        try
        {
            Hide();
            _windowHandle = new WindowInteropHelper(this).EnsureHandle();
            _capture.SetWindowCaptureExclusion(_windowHandle, true);
            _captureExcluded = true;
            var bounds = new ScreenRegion(anchor.X - 26, anchor.Y - 26, 52, 52);
            _windows.PlaceWindow(_windowHandle, bounds);
            Show();
            // 첫 Show 및 DPI 전환 뒤에도 클릭 대상의 물리 좌표를 다시 맞춘다.
            _windows.PlaceWindow(_windowHandle, bounds);
            return true;
        }
        catch (Exception exception)
        {
            Hide();
            FailureReason = $"렌즈 펼치기 아이콘을 준비하지 못했습니다: {exception.Message}";
            return false;
        }
    }

    private void ExpandButton_OnClick(object sender, RoutedEventArgs e)
    {
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
