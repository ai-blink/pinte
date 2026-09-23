using System.Windows;

namespace Magnifier.App;

public partial class MainWindow
{
    private async Task ShowRegionSettingsAsync(Window owner)
    {
        if (_returning || _shuttingDown || _modalOpen || _completingSource || _frame is null || _lens?.IsInteractionLocked == true) return;
        var version = _sessionVersion;
        var lensVisible = _lens?.IsVisible == true;
        try
        {
            await BeginModalAsync("크기·비율 설정 · 조작 일시 중지");
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            var settings = new QuickRegionSettingsWindow(_capture, _windows.DesktopBounds, _frame.Region,
                _frame.LockedAspectRatio, _settings.HideAppWindowsFromScreenCapture) { Owner = owner };
            settings.SizingChanged += options => _frame.ApplySizing(options.Width, options.Height, options.LockedAspectRatio);
            settings.ShowDialog();
            // 숨은 편집창의 RegionChanged는 구독 guard를 통과하지 않으므로 명시 반영한다.
            if (version == _sessionVersion && lensVisible && !_editingSource && !_returning && !_shuttingDown)
            {
                await ChangeSourceAsync(_frame.Region);
                _indicatorPolicyPending = true;
            }
            RememberLayout();
        }
        catch (Exception ex)
        {
            await StopAfterEditingFailureAsync($"크기·비율 설정 실패: {ex.Message}");
        }
        finally
        {
            await EndModalAsync(version, "크기·비율 설정 닫기 · 새 화면 확인 뒤 조작 자동 재개");
        }
    }

    private async Task ShowAppSettingsAsync(Window owner)
    {
        if (_returning || _shuttingDown || _modalOpen || _completingSource || _lens?.IsInteractionLocked == true) return;
        var version = _sessionVersion;
        var lensVisible = _lens?.IsVisible == true;
        try
        {
            await BeginModalAsync("앱 설정 · 조작 일시 중지");
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            var settings = new SettingsWindow(_settings, _capture, lensVisible && !_editingSource, TimingSummary) { Owner = owner };
            settings.SettingsChanged += ApplySettings;
            settings.ShowDialog();
            if (version == _sessionVersion && settings.ResizeLensRequested && !_returning && !_shuttingDown)
                _lens?.SetResizeControlsVisible(true);
            if (settings.InputTimingRequested && !_shuttingDown) OpenTimingWindow(owner);
        }
        catch (Exception ex)
        {
            await StopAfterEditingFailureAsync($"앱 설정 실패: {ex.Message}");
        }
        finally
        {
            await EndModalAsync(version, "앱 설정 닫기 · 새 화면 확인 뒤 조작 자동 재개");
        }
    }

    private void ApplySettings(MagnifierSettings settings)
    {
        var captureExclusionChanged = _settings.HideAppWindowsFromScreenCapture != settings.HideAppWindowsFromScreenCapture;
        if (_settings.SourceIndicatorPreference != settings.SourceIndicatorPreference || captureExclusionChanged)
        {
            _indicatorPolicyPending = true;
            HideSourceIndicator();
        }
        _settings = settings;
        MagnifierTheme.Apply(settings.Theme);
        _lens?.SetToolbarPlacement(settings.ToolbarPlacement);
        _lens?.SetDisplayMode(settings.LensDisplayMode);
        if (captureExclusionChanged) ApplyCaptureExclusion(settings.HideAppWindowsFromScreenCapture);
        if (!MagnifierSettingsStore.Save(settings))
            SelectionStatusText.Text = "설정 저장에 실패해 이번 실행에만 적용합니다.";
    }

    private void ApplyCaptureExclusion(bool excludeFromCapture)
    {
        try
        {
            _frame?.SetCaptureExclusion(excludeFromCapture);
            _lens?.SetCaptureExclusion(excludeFromCapture);
            _indicator?.SetCaptureExclusion(excludeFromCapture);
            _collapsedLens?.SetCaptureExclusion(excludeFromCapture);
        }
        catch (Exception exception)
        {
            SelectionStatusText.Text = $"화면 캡처 숨김 설정 적용 실패: {exception.Message}";
        }
    }

    private async Task BeginModalAsync(string reason)
    {
        _modalOpen = true;
        _captureVersion++;
        _lens?.SetInteractionLocked(true);
        UpdateSourceEditorEnabled();
        if (_lens?.IsVisible == true)
        {
            await _lens.SetInputSuspendedAsync(true, reason);
            await _lens.EndPanModeAsync();
        }
    }

    private async Task EndModalAsync(int version, string reason)
    {
        _modalOpen = false;
        _lens?.SetInteractionLocked(false);
        UpdateSourceEditorEnabled();
        if (version != _sessionVersion || _returning || _shuttingDown || _lens?.IsVisible != true) return;
        if (_indicatorPolicyPending) ApplySourceIndicatorPolicy();
        await ResumeInputAfterModalAsync(reason);
    }

    private async Task ResumeInputAfterModalAsync(string reason)
    {
        if (_lens is null) return;
        // Capture begun before the modal closed must not refresh the relay's new-frame gate.
        _captureVersion++;
        try
        {
            await _lens.RefreshGeometryAsync();
            _captureVersion++;
            await _lens.SetInputSuspendedAsync(_editingSource || _modalOpen, reason);
        }
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

}
