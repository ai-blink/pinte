using System.Windows;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private CancellationTokenSource? _straightStrokeCancellation;
    private ScreenPoint? _pointA, _pointB;
    private PreviewPoint? _pointAVisual, _pointBVisual;
    private PointTarget _pendingPointTarget;
    private int _countdownSeconds = 3;
    private bool _isSynchronizingInputToggle;
    private bool _resumeAfterAuxiliary;

    private async void AuxiliaryTools_OnExpanded(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        var resume = _relayStatus is { InputRequested: true };
        try { await StopAsync("A/B 보조 도구 · 직접 조작 꺼짐"); _resumeAfterAuxiliary = resume; }
        catch { }
        ResizeLens();
        QueueGeometryUpdate();
        UpdatePointMarkers();
        UpdateControls();
    }

    private async void AuxiliaryTools_OnCollapsed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        var resume = _resumeAfterAuxiliary;
        _pendingPointTarget = PointTarget.None;
        try { await StopAsync("A/B 보조 도구 닫기 · 보기로 전환"); }
        catch { resume = false; }
        ResizeLens();
        QueueGeometryUpdate();
        UpdatePointMarkers();
        UpdateControls();
        if (resume)
        {
            try { await StartInputAsync(); }
            catch (Exception exception) { PublishInputStatus($"조작 재개 실패: {exception.Message}"); }
        }
    }

    private void PreviewInputEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingInputToggle || !IsInitialized) return;
        if (PreviewInputEnabledCheckBox.IsChecked != true) CancelStraightStroke();
        try
        {
            var enabled = PreviewInputEnabledCheckBox.IsChecked == true && AuxiliaryTools.IsExpanded && _editingAllowed;
            _inputSession.SetInputEnabled(enabled);
            SetPreviewInputToggle(enabled);
            PublishInputStatus(enabled ? "A/B 실제 입력 허용 · 직접 조작은 꺼짐" : "A/B 실제 입력 꺼짐");
        }
        catch (Exception exception)
        {
            SetPreviewInputToggle(false);
            PublishInputStatus($"A/B 입력 상태 변경 실패: {exception.Message}");
        }
        UpdateStrokeControls();
    }

    private void SetPreviewInputToggle(bool isEnabled)
    {
        if (PreviewInputEnabledCheckBox.IsChecked == isEnabled) return;
        _isSynchronizingInputToggle = true;
        try { PreviewInputEnabledCheckBox.IsChecked = isEnabled; }
        finally { _isSynchronizingInputToggle = false; }
    }

    private void CountdownDecrease_OnClick(object sender, RoutedEventArgs e)
    {
        _countdownSeconds = Math.Max(1, _countdownSeconds - 1);
        UpdateStrokeControls();
    }

    private void CountdownIncrease_OnClick(object sender, RoutedEventArgs e)
    {
        _countdownSeconds = Math.Min(10, _countdownSeconds + 1);
        UpdateStrokeControls();
    }

    private void SelectPointA_OnClick(object sender, RoutedEventArgs e) => BeginPointSelection(PointTarget.A);
    private void SelectPointB_OnClick(object sender, RoutedEventArgs e) => BeginPointSelection(PointTarget.B);

    private void BeginPointSelection(PointTarget target)
    {
        if (!_editingAllowed || !AuxiliaryTools.IsExpanded) return;
        _pendingPointTarget = target;
        PublishInputStatus($"확대 이미지에서 {target} 지점을 선택하세요 · 실제 입력은 보내지 않습니다");
    }

    private void SelectPointFromImage(Point imagePoint)
    {
        if (!_editingAllowed || !TryMapToScreen(imagePoint, out var point, out var visualPoint)) return;
        if (_pendingPointTarget == PointTarget.A) { _pointA = point; _pointAVisual = visualPoint; }
        else { _pointB = point; _pointBVisual = visualPoint; }
        _pendingPointTarget = PointTarget.None;
        UpdateStrokeControls();
        UpdatePointMarkers();
        PublishInputStatus($"A/B 지점 지정 · X {point.X} · Y {point.Y}");
    }

    private async void RunStraightStroke_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_editingAllowed || !_inputSession.IsInputEnabled || _pointA is not ScreenPoint pointA || _pointB is not ScreenPoint pointB) return;
        var cancellation = new CancellationTokenSource();
        _straightStrokeCancellation = cancellation;
        _pendingPointTarget = PointTarget.None;
        UpdateControls();
        try
        {
            for (var remaining = _countdownSeconds; remaining > 0; remaining--)
            {
                PublishInputStatus($"{remaining}초 뒤 A→B 직선 · 조작 중지 또는 원래 화면으로 취소");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            Hide();
            try
            {
                if (_inputSession.Begin(pointA))
                {
                    _inputSession.Complete(pointB);
                    PublishInputStatus("A/B 입력 API 수락 · 버튼 해제 · 실제 대상 반응을 확인하세요");
                }
            }
            finally { if (!_closed) Show(); }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { PublishInputStatus("A/B 카운트다운 취소 · 실제 입력 꺼짐"); }
        catch (Exception exception)
        { PublishInputStatus($"A/B 입력 실패: {exception.Message}"); }
        finally
        {
            try { _inputSession.SetInputEnabled(false); }
            catch (Exception exception) { PublishInputStatus($"A/B 해제 재시도 필요: {exception.Message}"); }
            SetPreviewInputToggle(false);
            if (ReferenceEquals(_straightStrokeCancellation, cancellation)) _straightStrokeCancellation = null;
            cancellation.Dispose();
            UpdateControls();
        }
    }

    private void CancelStraightStroke() => _straightStrokeCancellation?.Cancel();

    private void ClearPoints()
    {
        _pointA = _pointB = null;
        _pointAVisual = _pointBVisual = null;
        _pendingPointTarget = PointTarget.None;
        UpdateStrokeControls();
        UpdatePointMarkers();
    }

    private void UpdateStrokeControls()
    {
        if (!IsInitialized) return;
        CountdownText.Text = $"{_countdownSeconds}초";
        PointStatusText.Text = $"A {FormatPoint(_pointA)} · B {FormatPoint(_pointB)}";
        RunStraightStrokeButton.IsEnabled = _inputSession.IsInputEnabled && _pointA.HasValue && _pointB.HasValue
            && _straightStrokeCancellation is null && _bitmap is not null;
    }

    private static string FormatPoint(ScreenPoint? point) => point is ScreenPoint value ? $"({value.X}, {value.Y})" : "미지정";
    private enum PointTarget { None, A, B }
}
