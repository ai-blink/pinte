using System.Windows.Threading;

namespace Magnifier.App;

public partial class MainWindow
{
    private SourceIndicatorWindow? _indicator;
    private readonly SourceIndicatorLifetime _indicatorLifetime;
    private readonly DispatcherTimer _indicatorTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _editingSource, _completingSource, _modalOpen, _indicatorPolicyPending;

    private void HideSourceIndicator()
    {
        _indicatorTimer.Stop();
        _indicatorLifetime.Cancel();
        _indicator?.Hide();
    }

    private void ApplySourceIndicatorPolicy()
    {
        HideSourceIndicator();
        if (_returning || _shuttingDown || _selecting || _editingSource || _modalOpen ||
            _lens?.IsVisible != true || _region is not { } source) return;
        _indicatorPolicyPending = false;
        _indicatorLifetime.Start(_settings.SourceIndicatorPreference);
        if (!_indicatorLifetime.IsVisible) return;
        if (_indicator?.ShowRegion(source) != true)
        {
            var reason = _indicator?.FailureReason ?? "표시창을 준비하지 못했습니다.";
            HideSourceIndicator();
            _lens.ShowSourceIndicatorFailure($"원본 윤곽선을 숨겼습니다: {reason}");
            SelectionStatusText.Text = $"원본 윤곽선 표시 실패: {reason}";
            return;
        }
        _lens.ShowSourceIndicatorFailure(null);
        if (_indicatorLifetime.IsBrief) _indicatorTimer.Start();
    }

    private void UpdateIndicatorExpiry()
    {
        if (!_indicatorLifetime.IsVisible) HideSourceIndicator();
    }

    private void UpdateSourceEditorEnabled() =>
        _frame?.SetEditingEnabled((_selecting || _editingSource) && !_modalOpen && !_completingSource);

    private async Task BeginSourceEditingAsync()
    {
        if (_editingSource || _selecting || _openingLens || _returning || _shuttingDown ||
            _modalOpen || _lens?.IsVisible != true || _frame is null) return;
        var version = _sessionVersion;
        _editingSource = true;
        _captureVersion++;
        _timer.Stop();
        HideSourceIndicator();
        try
        {
            _lens.SetSourceEditing(true);
            // 손 도구 해제가 내부 보류를 풀더라도 외부 편집 보류는 유지한다.
            await _lens.SetInputSuspendedAsync(true, "원본 영역 편집 · 실제 입력 해제");
            await _lens.EndPanModeAsync();
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            _frame.SetEditingEnabled(true);
            _frame.SetSourceEditingMode();
            _frame.ShowEditor();
            if (_region is { } source) _frame.SetRegion(source);
            _frame.Activate();
        }
        catch (Exception exception)
        {
            if (version != _sessionVersion) return;
            await StopAfterEditingFailureAsync($"영역 편집 시작 실패: {exception.Message}");
        }
    }

    private async Task CompleteSourceEditingAsync()
    {
        if (!_editingSource || _completingSource || _modalOpen || _returning || _shuttingDown ||
            _frame is null || _lens is null) return;
        var version = _sessionVersion;
        _completingSource = true;
        UpdateSourceEditorEnabled();
        try
        {
            var source = _frame.Region;
            _frame.Hide();
            _captureVersion++;
            await ChangeSourceAsync(source);
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            await _lens.RefreshGeometryAsync();
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            _editingSource = false;
            _lens.SetSourceEditing(false);
            ApplySourceIndicatorPolicy();
            _captureVersion++;
            await _lens.SetInputSuspendedAsync(_modalOpen, "영역 편집 완료 · 버튼 해제와 새 화면 뒤 자동 재개");
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            if (_captureFinished is { } pending) await pending.Task;
            if (version != _sessionVersion || _returning || _shuttingDown) return;
            await CaptureOnceAsync();
            if (version == _sessionVersion && !_returning && !_shuttingDown) _timer.Start();
            RememberLayout();
        }
        catch (Exception exception)
        {
            if (version != _sessionVersion) return;
            await StopAfterEditingFailureAsync($"영역 편집 완료 실패: {exception.Message}");
        }
        finally
        {
            _completingSource = false;
            UpdateSourceEditorEnabled();
        }
    }

    private async Task StopAfterEditingFailureAsync(string reason)
    {
        HideSourceIndicator();
        try { if (_lens is not null) await _lens.StopAsync(reason); }
        catch (Exception exception) { reason += $" · 입력 해제 재시도 필요: {exception.Message}"; }
        _editingSource = false;
        _frame?.Hide();
        _lens?.SetSourceEditing(false);
        _lens?.ShowSourceIndicatorFailure(reason);
        if (_selecting && _lens?.IsVisible != true)
        {
            _selecting = false;
            _openingLens = false;
            Show();
            Activate();
        }
        SelectionStatusText.Text = reason;
        // 재개 요청을 생성하지 않는다. 복귀/다시 확대만 새 요청을 만든다.
    }
}
