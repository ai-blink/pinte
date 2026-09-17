using System.Windows;
using System.Windows.Interop;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SourceIndicatorWindow : Window
{
    private readonly IScreenCapture _capture;
    private readonly IWindowEnvironment _windows;
    private nint _windowHandle;
    private bool _captureExcluded;
    private bool _closed;
    private bool _hideFromScreenCapture;

    public SourceIndicatorWindow(IScreenCapture capture, IWindowEnvironment windows, bool hideFromScreenCapture = false)
    {
        InitializeComponent();
        _capture = capture;
        _windows = windows;
        _hideFromScreenCapture = hideFromScreenCapture;
    }

    // 실패 상태는 Hide나 후속 표시 요청으로 지우지 않는다.
    public string? FailureReason { get; private set; }

    public void SetCaptureExclusion(bool excludeFromCapture)
    {
        _hideFromScreenCapture = excludeFromCapture;
        // 새 정책은 이전 정책의 실패를 고정하지 않는다. 다음 표시 요청에서 다시 판정한다.
        FailureReason = null;
        if (_windowHandle == nint.Zero) return;
        _capture.SetWindowCaptureExclusion(_windowHandle, excludeFromCapture);
        _captureExcluded = excludeFromCapture;
    }

    public bool ShowRegion(ScreenRegion region)
    {
        if (_closed || FailureReason is not null) return false;

        try
        {
            Hide();
            _windowHandle = new WindowInteropHelper(this).EnsureHandle();
            _windows.SetPassiveOverlay(_windowHandle);
            SetCaptureExclusion(_hideFromScreenCapture);
            // 물리 픽셀 배치는 Infrastructure가 소유한다. 원본 영역을 DIP로 다시 저장하지 않는다.
            _windows.PlaceWindow(_windowHandle, region);
            Show();
            // 첫 Show 및 다른 DPI 모니터로 이동한 WPF의 크기 갱신 뒤 원본 경계를 다시 맞춘다.
            _windows.PlaceWindow(_windowHandle, region);
            return true;
        }
        catch (Exception exception)
        {
            Hide();
            FailureReason = $"원본 영역 윤곽선을 숨겼습니다: {exception.Message}";
            return false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        try
        {
            if (_captureExcluded) _capture.SetWindowCaptureExclusion(_windowHandle, false);
        }
        catch (Exception exception)
        {
            FailureReason ??= $"원본 윤곽선의 캡처 제외 해제에 실패했습니다: {exception.Message}";
        }
        finally
        {
            _captureExcluded = false;
            base.OnClosed(e);
        }
    }
}
