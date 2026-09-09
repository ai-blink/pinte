using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Magnifier.Core;
using Magnifier.Infrastructure;

namespace Magnifier.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IScreenCapture _screenCapture = new WindowsScreenCapture();
    private readonly IPointerInput _pointerInput = new WindowsPointerInput();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly DispatcherTimer _livePreviewTimer;
    private SelectionPreviewWindow? _previewWindow;
    private ScreenRegion? _activePreviewRegion;
    private bool _captureInProgress;
    private bool _isInputEnabled;
    private int _livePreviewSession;

    public MainWindow()
    {
        InitializeComponent();
        _livePreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _livePreviewTimer.Tick += LivePreviewTimer_OnTick;
        Closed += (_, _) =>
        {
            _previewWindow?.CancelInputSession();
            StopLivePreview();
        };
    }

    private async void SelectRegionButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopLivePreview();
        _previewWindow?.CancelInputSession();
        _previewWindow?.Hide();

        var overlay = new SelectionOverlayWindow
        {
            Owner = this
        };

        overlay.ShowDialog();

        if (overlay.SelectedRegion is not ScreenRegion selectionRegion)
        {
            SelectionStatusText.Text = "영역 선택이 취소되었습니다";
            return;
        }

        await StartLivePreviewAsync(selectionRegion);
    }

    private void InputEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        var isInputEnabled = InputEnabledCheckBox.IsChecked == true;
        if (isInputEnabled && _previewWindow is null)
        {
            _isInputEnabled = true;
            SelectionStatusText.Text = "입력 전달을 켰습니다. 영역을 고르면 바로 사용할 수 있습니다";
            return;
        }

        if (_previewWindow is not null && !_previewWindow.SetInputEnabled(isInputEnabled))
        {
            _isInputEnabled = false;
            InputEnabledCheckBox.IsChecked = false;
            return;
        }

        _isInputEnabled = isInputEnabled;
        SelectionStatusText.Text = isInputEnabled
            ? "실제 포인터 입력이 켜졌습니다. 짧은 드래그만 사용하세요"
            : "실제 포인터 입력이 꺼져 있습니다";
    }

    private async Task StartLivePreviewAsync(ScreenRegion selectionRegion)
    {
        StopLivePreview();
        _activePreviewRegion = selectionRegion;
        var session = _livePreviewSession;
        SelectionStatusText.Text = "선택 영역의 실시간 캡처를 시작하는 중입니다";

        await CaptureAndUpdatePreviewAsync(selectionRegion, session, startTimerOnSuccess: true);
    }

    private async void LivePreviewTimer_OnTick(object? sender, EventArgs e)
    {
        if (_activePreviewRegion is not ScreenRegion selectionRegion)
        {
            StopLivePreview();
            return;
        }

        if (_previewWindow is null || !_previewWindow.IsVisible)
        {
            StopLivePreview();
            return;
        }

        if (_captureInProgress)
        {
            return;
        }

        await CaptureAndUpdatePreviewAsync(selectionRegion, _livePreviewSession, startTimerOnSuccess: false);
    }

    private async Task CaptureAndUpdatePreviewAsync(
        ScreenRegion selectionRegion,
        int session,
        bool startTimerOnSuccess)
    {
        await _captureGate.WaitAsync();
        _captureInProgress = true;

        try
        {
            if (!IsActivePreviewSession(selectionRegion, session))
            {
                return;
            }

            var frame = await Task.Run(() => _screenCapture.Capture(selectionRegion));

            if (!IsActivePreviewSession(selectionRegion, session))
            {
                return;
            }

            ShowPreview(selectionRegion);
            _previewWindow!.UpdateCapture(frame, isLivePreview: true);

            if (startTimerOnSuccess)
            {
                SelectionStatusText.Text = $"실시간 미리보기: {selectionRegion.Width} × {selectionRegion.Height}";
                _livePreviewTimer.Start();
            }
        }
        catch (Exception exception)
        {
            if (!IsActivePreviewSession(selectionRegion, session))
            {
                return;
            }

            StopLivePreview();
            _previewWindow?.CancelInputSession();
            ShowPreview(selectionRegion);
            _previewWindow!.ShowCaptureFailure(selectionRegion, exception.Message);
            SelectionStatusText.Text = "실시간 화면 캡처에 실패했습니다";
        }
        finally
        {
            _captureInProgress = false;
            _captureGate.Release();
        }
    }

    private bool IsActivePreviewSession(ScreenRegion selectionRegion, int session)
    {
        return _livePreviewSession == session && _activePreviewRegion == selectionRegion;
    }

    private void StopLivePreview()
    {
        _livePreviewTimer.Stop();
        _activePreviewRegion = null;
        _livePreviewSession++;
    }

    private void PreviewWindow_OnInputStatusChanged(string status)
    {
        SelectionStatusText.Text = status;

        if (_previewWindow is not null && InputEnabledCheckBox.IsChecked != _previewWindow.IsInputEnabled)
        {
            InputEnabledCheckBox.IsChecked = _previewWindow.IsInputEnabled;
        }

        _isInputEnabled = _previewWindow?.IsInputEnabled == true;
    }

    private void ShowPreview(ScreenRegion selectionRegion)
    {
        if (_previewWindow is null)
        {
            var previewWindow = new SelectionPreviewWindow(_pointerInput, _screenCapture)
            {
                Owner = this
            };
            previewWindow.InputStatusChanged += PreviewWindow_OnInputStatusChanged;
            previewWindow.Closed += (_, _) =>
            {
                if (ReferenceEquals(_previewWindow, previewWindow))
                {
                    _previewWindow = null;
                    _isInputEnabled = false;
                    InputEnabledCheckBox.IsChecked = false;
                    StopLivePreview();
                }
            };
            _previewWindow = previewWindow;
            _previewWindow.SetInputEnabled(_isInputEnabled);
            _previewWindow.Show();
        }

        if (!_previewWindow.IsVisible)
        {
            _previewWindow.Show();
        }

    }
}
