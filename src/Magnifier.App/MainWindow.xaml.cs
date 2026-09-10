using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Magnifier.Core;
using Magnifier.Infrastructure;

namespace Magnifier.App;

// Composition root: windows below this boundary consume Core contracts only.
public partial class MainWindow : Window
{
    private readonly IScreenCapture _capture = new WindowsScreenCapture();
    private readonly IWindowEnvironment _windows = new WindowsWindowEnvironment();
    private readonly IPointerInput _pointer = new WindowsPointerInput();
    private readonly ILivePointerRelay _relay = new WindowsLivePointerRelay();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private SelectionOverlayWindow? _frame;
    private SelectionPreviewWindow? _lens;
    private ScreenRegion? _region;
    private bool _capturing, _returning, _shuttingDown, _closed;
    private int _captureVersion;
    private LensLayout? _layout;

    public MainWindow()
    {
        InitializeComponent();
        _layout = LensLayoutStore.Load();
        _timer.Tick += CaptureTick;
        Closing += MainClosing;
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
    }

    private async void SelectRegionButton_OnClick(object sender, RoutedEventArgs e)
        => await OpenMagnifierAsync(placeLower: false);

    private async void LowerPlacementButton_OnClick(object sender, RoutedEventArgs e)
        => await OpenMagnifierAsync(placeLower: true);

    private async Task OpenMagnifierAsync(bool placeLower)
    {
        SelectRegionButton.IsEnabled = false;
        LowerPlacementButton.IsEnabled = false;
        var savedLayout = _layout;
        try
        {
            await _relay.StopAsync("확대 시작 · 화면 준비");
            EnsureWindows();
            _frame!.Show();
            _lens!.Show();
            var work = _windows.GetWindowWorkArea(new WindowInteropHelper(this).Handle);
            var source = savedLayout?.Source;
            if (source is null || !_windows.IsRegionVisible(source.Value))
            {
                placeLower = true;
                source = new ScreenRegion(work.X + work.Width / 4, work.Y + work.Height / 2,
                    Math.Min(320, work.Width / 3), Math.Min(220, work.Height / 3));
            }
            _frame.SetRegion(source.Value);
            if (savedLayout is { } layout && _windows.IsRegionVisible(layout.Lens))
                _windows.PlaceWindow(_lens.WindowHandle, layout.Lens);
            else
            {
                placeLower = true;
                _windows.PlaceWindow(_lens.WindowHandle, new ScreenRegion(work.X + work.Width / 3,
                    work.Y + work.Height / 3, Math.Min(800, work.Width * 2 / 3), Math.Min(640, work.Height - 60)));
            }
            _lens.SetZoom(savedLayout?.Zoom ?? 2);
            await ChangeSourceAsync(_frame.Region);
            if (placeLower)
            {
                // Measure complete native windows after layout, including DPI-scaled title bars.
                var pair = WindowPairPlacement.Lower(work, _windows.GetWindowBounds(_frame.WindowHandle),
                    _windows.GetWindowBounds(_lens.WindowHandle));
                _windows.PlaceWindow(_frame.WindowHandle, pair.Frame);
                _windows.PlaceWindow(_lens.WindowHandle, pair.Lens);
                await ChangeSourceAsync(_frame.Region);
            }
            Hide();
            _timer.Start();
            await CaptureOnceAsync();
            await _lens.StartInputAsync();
        }
        catch (Exception ex)
        {
            await ReturnToScreenAsync();
            SelectionStatusText.Text = $"확대 시작 실패: {ex.Message}";
        }
        finally { SelectRegionButton.IsEnabled = true; LowerPlacementButton.IsEnabled = true; }
    }

    private void EnsureWindows()
    {
        if (_frame is not null && _lens is not null) return;
        // Hiding an owner also hides its owned windows. These two windows must stay
        // visible while the entry window is hidden; their lifetime is managed below.
        _frame = new SelectionOverlayWindow(_capture);
        _lens = new SelectionPreviewWindow(_relay, _capture, _windows, _pointer);
        _frame.RegionChanged += async region =>
        {
            if (_returning || _shuttingDown) return;
            try { await ChangeSourceAsync(region); }
            catch (Exception ex) { SelectionStatusText.Text = ex.Message; }
        };
        _frame.AdjustmentStarted += async () =>
        {
            try { await _lens.PauseAsync("원본 영역 조절 · 버튼 해제 후 자동 재개"); }
            catch (Exception ex) { SelectionStatusText.Text = ex.Message; }
        };
        _frame.ReturnRequested += async () => await ReturnToScreenAsync();
        _lens.ReturnRequested += async () => await ReturnToScreenAsync();
        _lens.EditingAllowedChanged += allowed => _frame.SetEditingEnabled(allowed);
        _lens.InputStatusChanged += text => SelectionStatusText.Text = text;
        _lens.PlacementChanged += RememberLayout;
        _frame.Closing += PreventSecondaryClose;
        _lens.Closing += PreventSecondaryClose;
    }

    private void PreventSecondaryClose(object? sender, CancelEventArgs e)
    {
        if (_shuttingDown) return;
        e.Cancel = true;
        _ = ReturnToScreenAsync();
    }

    private async Task ChangeSourceAsync(ScreenRegion region)
    {
        if (_lens is null || _frame is null) return;
        if (_region != region) { _region = region; _captureVersion++; }
        await _lens.SetSourceAsync(region, _frame.WindowHandle);
        RememberLayout();
    }

    private async void CaptureTick(object? sender, EventArgs e) => await CaptureOnceAsync();

    private async Task CaptureOnceAsync()
    {
        if (_capturing || _region is not { } region || _lens?.IsVisible != true || _returning) return;
        var version = _captureVersion;
        _capturing = true;
        try
        {
            if (!_windows.IsRegionVisible(region)) throw new InvalidOperationException("원본 테두리를 연결된 화면 안으로 옮기세요.");
            var frame = await Task.Run(() => _capture.Capture(region));
            if (version != _captureVersion || _returning || _lens?.IsVisible != true) return;
            _lens.UpdateCapture(frame, true);
        }
        catch (Exception ex)
        {
            if (version == _captureVersion && !_returning && _lens is not null)
                _lens.ShowCaptureFailure(region, ex.Message);
        }
        finally { _capturing = false; }
    }

    private void RememberLayout()
    {
        if (_region is not { } region || _lens?.WindowHandle is not { } handle || handle == 0) return;
        _layout = new(region, _windows.GetWindowBounds(handle), _lens.Zoom);
    }

    private async Task<bool> ReturnToScreenAsync()
    {
        if (_returning || _shuttingDown) return false;
        _returning = true;
        try
        {
            // Release -> disable -> hide both -> restore entry. A new open requests input anew.
            if (_lens is not null) await _lens.StopAsync("원래 화면 · 입력 꺼짐");
            else await _relay.StopAsync("원래 화면 · 입력 꺼짐");
            _timer.Stop();
            _captureVersion++;
            RememberLayout();
            _frame?.Hide();
            _lens?.Hide();
            Show();
            Activate();
            SelectionStatusText.Text = "원래 화면으로 돌아왔습니다. 확대 시작을 누르면 바로 조작할 수 있습니다.";
            if (_layout is not null && !LensLayoutStore.Save(_layout))
                SelectionStatusText.Text += " 배치는 이번 실행에서만 기억합니다.";
            return true;
        }
        catch (Exception ex) { Show(); SelectionStatusText.Text = $"복귀 중 오류: {ex.Message}"; return false; }
        finally { _returning = false; }
    }

    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == 0x007E) Dispatcher.BeginInvoke(async () =>
        {
            if (!await ReturnToScreenAsync()) return;
            var handle = new WindowInteropHelper(this).Handle;
            var work = _windows.GetWindowWorkArea(handle);
            var bounds = _windows.GetWindowBounds(handle);
            var width = Math.Min(bounds.Width, work.Width);
            var height = Math.Min(bounds.Height, work.Height);
            _windows.PlaceWindow(handle, new ScreenRegion(
                Math.Clamp(bounds.X, work.X, work.X + work.Width - width),
                Math.Clamp(bounds.Y, work.Y, work.Y + work.Height - height), width, height));
            SelectionStatusText.Text = "화면 구성이 바뀌어 조작을 중지했습니다. 확대를 다시 시작하세요.";
        });
        return 0;
    }

    private async void MainClosing(object? sender, CancelEventArgs e)
    {
        if (_closed) return;
        e.Cancel = true;
        if (_shuttingDown) return;
        await ReturnToScreenAsync();
        _shuttingDown = true;
        try
        {
            await _relay.DisposeAsync();
            _frame?.Close();
            _lens?.Close();
            _closed = true;
            Close();
        }
        catch (Exception ex)
        {
            _shuttingDown = false;
            Show();
            SelectionStatusText.Text = $"종료 전 입력 해제가 필요합니다: {ex.Message}";
        }
    }
}
