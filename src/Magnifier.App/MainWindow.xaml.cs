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
    private bool _selecting, _openingLens;
    private int _captureVersion, _sessionVersion;
    private LensLayout? _layout;
    private MagnifierSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = MagnifierSettingsStore.Load();
        MagnifierTheme.Apply(_settings.Theme);
        _layout = LensLayoutStore.Load();
        _timer.Tick += CaptureTick;
        Closing += MainClosing;
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
    }

    private async void SelectRegionButton_OnClick(object sender, RoutedEventArgs e)
        => await SelectRegionAsync();

    private async Task SelectRegionAsync()
    {
        if (_selecting || _openingLens || _returning || _shuttingDown) return;
        var version = ++_sessionVersion;
        _selecting = true;
        SelectRegionButton.IsEnabled = false;
        var savedLayout = _layout;
        try
        {
            _timer.Stop();
            if (_lens is not null) await _lens.StopAsync("영역 지정 · 입력 꺼짐");
            else await _relay.StopAsync("영역 지정 · 입력 꺼짐");
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            EnsureWindows();
            _lens!.Hide();
            _frame!.SetEditingEnabled(true);
            _frame.SetSelectionMode(true);
            _frame!.Show();
            var work = _windows.GetWindowWorkArea(new WindowInteropHelper(this).Handle);
            var source = _settings.RememberLayout ? savedLayout?.Source : null;
            if (source is null || !_windows.IsRegionVisible(source.Value))
            {
                var requested = new ScreenRegion(work.X + (work.Width - _settings.DefaultSourceWidth) / 2,
                    work.Y + (work.Height - _settings.DefaultSourceHeight) / 2,
                    _settings.DefaultSourceWidth, _settings.DefaultSourceHeight);
                source = ScreenRegionSizing.Fit(requested, work);
            }
            _frame.ApplySizing(source.Value.Width, source.Value.Height,
                _settings.RememberLayout ? savedLayout?.LockedAspectRatio : null);
            _frame.SetRegion(source.Value);
            _region = _frame.Region;
            _captureVersion++;
            Hide();
            SelectionStatusText.Text = "테두리를 옮기고 크기를 맞춘 뒤 ‘이 영역 확대’를 누르세요.";
        }
        catch (Exception ex)
        {
            if (version != _sessionVersion) return;
            await ReturnToScreenAsync();
            SelectionStatusText.Text = $"영역 지정 실패: {ex.Message}";
        }
        finally { SelectRegionButton.IsEnabled = true; }
    }

    private async Task OpenSelectedRegionAsync()
    {
        if (!_selecting || _openingLens || _returning || _shuttingDown || _frame is null || _lens is null) return;
        var version = _sessionVersion;
        var savedLayout = _layout;
        _selecting = false;
        _openingLens = true;
        _frame.SetSelectionMode(false);
        try
        {
            _lens.Show();
            var work = _windows.GetWindowWorkArea(_frame.WindowHandle);
            if (_settings.RememberLayout && savedLayout is { } layout && _windows.IsRegionVisible(layout.Lens))
                _windows.PlaceWindow(_lens.WindowHandle, layout.Lens);
            else
                _windows.PlaceWindow(_lens.WindowHandle, new ScreenRegion(work.X + work.Width / 3,
                    work.Y + work.Height / 3, Math.Min(800, work.Width * 2 / 3), Math.Min(640, work.Height - 60)));
            _lens.SetZoom(_settings.RememberLayout ? savedLayout?.Zoom ?? _settings.DefaultZoom : _settings.DefaultZoom);
            // The confirmed source stays exactly where the user placed it.
            await ChangeSourceAsync(_frame.Region);
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            _timer.Start();
            await CaptureOnceAsync();
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            await _lens.StartInputAsync();
        }
        catch (Exception ex)
        {
            if (version != _sessionVersion) return;
            await ReturnToScreenAsync();
            SelectionStatusText.Text = $"확대 시작 실패: {ex.Message}";
        }
        finally
        {
            if (version == _sessionVersion)
            {
                _openingLens = false;
                RememberLayout();
            }
        }
    }

    private void EnsureWindows()
    {
        if (_frame is not null && _lens is not null) return;
        // Hiding an owner also hides its owned windows. These two windows must stay
        // visible while the entry window is hidden; their lifetime is managed below.
        _frame = new SelectionOverlayWindow(_capture, _windows);
        _lens = new SelectionPreviewWindow(_relay, _capture, _windows, _pointer);
        _lens.SetToolbarPlacement(_settings.ToolbarPlacement);
        _frame.RegionChanged += async region =>
        {
            if (_returning || _shuttingDown || _frame.IsVisible != true) return;
            if (_selecting)
            {
                _region = region;
                _captureVersion++;
                return;
            }
            try { await ChangeSourceAsync(region); }
            catch (Exception ex) { SelectionStatusText.Text = ex.Message; }
        };
        _frame.AdjustmentStarted += async () =>
        {
            if (_selecting || _returning || _shuttingDown) return;
            try { await _lens.PauseAsync("원본 영역 조절 · 버튼 해제 후 자동 재개"); }
            catch (Exception ex) { SelectionStatusText.Text = ex.Message; }
        };
        _frame.RegionConfirmed += async () => await OpenSelectedRegionAsync();
        _frame.ReturnRequested += async () => await ReturnToScreenAsync();
        _frame.RegionSettingsRequested += async () => await ShowRegionSettingsAsync(_frame);
        _frame.AppSettingsRequested += async () => await ShowAppSettingsAsync(_frame);
        _lens.ReturnRequested += async () => await ReturnToScreenAsync();
        _lens.RegionSettingsRequested += async () => await ShowRegionSettingsAsync(_lens);
        _lens.AppSettingsRequested += async () => await ShowAppSettingsAsync(_lens);
        _lens.EditingAllowedChanged += allowed => _frame.SetEditingEnabled(_selecting || allowed);
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
        if (_selecting || _openingLens || _lens?.IsVisible != true) return;
        if (_region is not { } region || _lens?.WindowHandle is not { } handle || handle == 0) return;
        _layout = new(region, _windows.GetWindowBounds(handle), _lens.Zoom, _frame?.LockedAspectRatio);
    }

    private async Task ShowRegionSettingsAsync(Window owner)
    {
        if (_returning || _shuttingDown || _frame is null || _lens?.IsInteractionLocked == true) return;
        var lensVisible = _lens?.IsVisible == true;
        _lens?.SetInteractionLocked(true);
        _frame.SetEditingEnabled(false);
        try
        {
            if (lensVisible) await _lens!.SetInputSuspendedAsync(true, "크기·비율 설정 · 조작 일시 중지");
            var settings = new QuickRegionSettingsWindow(_capture, _windows.DesktopBounds, _frame.Region, _frame.LockedAspectRatio) { Owner = owner };
            settings.SizingChanged += options => _frame.ApplySizing(options.Width, options.Height, options.LockedAspectRatio);
            settings.ShowDialog();
            RememberLayout();
        }
        catch (Exception ex)
        {
            SelectionStatusText.Text = $"크기·비율 설정 실패: {ex.Message}";
        }
        finally
        {
            _lens?.SetInteractionLocked(false);
            if (_lens is null || !lensVisible) _frame.SetEditingEnabled(true);
            if (lensVisible && !_returning && !_shuttingDown)
            {
                await ResumeInputAfterModalAsync("크기·비율 설정 닫기 · 새 화면 확인 뒤 조작 자동 재개");
            }
        }
    }

    private async Task ShowAppSettingsAsync(Window owner)
    {
        if (_returning || _shuttingDown || _lens?.IsInteractionLocked == true) return;
        var lensVisible = _lens?.IsVisible == true;
        _lens?.SetInteractionLocked(true);
        _frame?.SetEditingEnabled(false);
        try
        {
            if (lensVisible) await _lens!.SetInputSuspendedAsync(true, "앱 설정 · 조작 일시 중지");
            var settings = new SettingsWindow(_settings, _capture) { Owner = owner };
            settings.SettingsChanged += ApplySettings;
            settings.ShowDialog();
        }
        catch (Exception ex)
        {
            SelectionStatusText.Text = $"앱 설정 실패: {ex.Message}";
        }
        finally
        {
            _lens?.SetInteractionLocked(false);
            if (_lens is null || !lensVisible) _frame?.SetEditingEnabled(true);
            if (lensVisible && !_returning && !_shuttingDown)
            {
                await ResumeInputAfterModalAsync("앱 설정 닫기 · 새 화면 확인 뒤 조작 자동 재개");
            }
        }
    }

    private void ApplySettings(MagnifierSettings settings)
    {
        _settings = settings;
        MagnifierTheme.Apply(settings.Theme);
        _lens?.SetToolbarPlacement(settings.ToolbarPlacement);
        if (!MagnifierSettingsStore.Save(settings))
            SelectionStatusText.Text = "설정 저장에 실패해 이번 실행에만 적용합니다.";
    }

    private async Task ResumeInputAfterModalAsync(string reason)
    {
        if (_lens is null) return;
        // Capture begun before the modal closed must not refresh the relay's new-frame gate.
        _captureVersion++;
        try { await _lens.SetInputSuspendedAsync(false, reason); }
        catch (Exception exception)
        {
            try { await _lens.StopAsync("설정 종료 뒤 조작 재개 실패 · 입력 해제"); }
            catch (Exception releaseException)
            {
                SelectionStatusText.Text = $"입력 해제 재시도 필요: {releaseException.Message}";
                return;
            }
            SelectionStatusText.Text = $"조작 재개 준비 실패 · 입력을 껐습니다: {exception.Message}";
        }
    }

    private async Task<bool> ReturnToScreenAsync()
    {
        if (_returning || _shuttingDown) return false;
        _returning = true;
        _sessionVersion++;
        _timer.Stop();
        _captureVersion++;
        try
        {
            // Release -> disable -> hide both -> restore entry. A new open requests input anew.
            if (_lens is not null) await _lens.StopAsync("원래 화면 · 입력 꺼짐");
            else await _relay.StopAsync("원래 화면 · 입력 꺼짐");
            RememberLayout();
            _selecting = false;
            _openingLens = false;
            _frame?.Hide();
            _lens?.Hide();
            Show();
            Activate();
            SelectionStatusText.Text = "원래 화면으로 돌아왔습니다. 화면 영역을 지정한 뒤 확대하세요.";
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
