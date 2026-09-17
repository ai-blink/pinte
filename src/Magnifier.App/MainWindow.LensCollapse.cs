using System.Windows;
using Magnifier.Core;

namespace Magnifier.App;

public partial class MainWindow
{
    private LensCollapseOverlayWindow? _collapsedLens;

    private void CreateCollapsedLensOverlay()
    {
        _collapsedLens = new LensCollapseOverlayWindow(_capture, _windows, _settings.HideAppWindowsFromScreenCapture);
        _collapsedLens.ExpandRequested += async () => await ExpandCollapsedLensAsync();
        _collapsedLens.Closing += PreventSecondaryClose;
    }

    private async Task HideLensAsync(ScreenPoint anchor)
    {
        if (_lensHidden || _selecting || _openingLens || _editingSource || _modalOpen || _returning || _shuttingDown ||
            _lens?.IsVisible != true || _collapsedLens is null) return;

        _timer.Stop();
        HideSourceIndicator();
        _captureVersion++;
        try
        {
            await _lens.StopAsync("렌즈 숨김 · 실제 입력 해제");
            if (_returning || _shuttingDown) return;
            RememberLayout();
            if (!_collapsedLens.ShowAt(anchor))
                throw new InvalidOperationException(_collapsedLens.FailureReason ?? "렌즈 펼치기 아이콘을 표시하지 못했습니다.");
            _lens.Hide();
            _lensHidden = true;
        }
        catch (Exception exception)
        {
            _lensHidden = false;
            _collapsedLens.Hide();
            try
            {
                if (_lens?.IsVisible == true)
                {
                    await _lens.RefreshGeometryAsync();
                    await CaptureOnceAsync();
                    await _lens.StartInputAsync();
                    _timer.Start();
                    _lens.ShowInteractionNotice($"렌즈 숨김 실패 · 계속 표시합니다: {exception.Message}");
                }
            }
            catch (Exception resumeException)
            {
                _lens?.ShowInteractionNotice($"렌즈 숨김 실패 후 조작 재개 실패: {resumeException.Message}");
            }
        }
    }

    private async Task ExpandCollapsedLensAsync()
    {
        if (!_lensHidden || _lens is null || _collapsedLens?.IsVisible != true || _selecting || _openingLens ||
            _editingSource || _modalOpen || _returning || _shuttingDown) return;

        var version = _sessionVersion;
        _openingLens = true;
        try
        {
            _lens.Show();
            _lens.Activate();
            await _lens.RefreshGeometryAsync();
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            await CaptureOnceAsync();
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            await _lens.StartInputAsync();
            if (version == _sessionVersion && !_returning && !_shuttingDown) _timer.Start();
            ApplySourceIndicatorPolicy();
            RememberLayout();
            _collapsedLens.Hide();
            _lensHidden = false;
        }
        catch (Exception exception)
        {
            _timer.Stop();
            _captureVersion++;
            try { await _lens.StopAsync("렌즈 펼치기 실패 · 실제 입력 해제"); }
            catch { }
            _lens.Hide();
            _lensHidden = true;
            _collapsedLens.Show();
            _lens.ShowInteractionNotice($"렌즈 펼치기 실패: {exception.Message}");
        }
        finally { _openingLens = false; }
    }
}
