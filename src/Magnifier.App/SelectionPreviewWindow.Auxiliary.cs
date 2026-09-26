using System.Windows;
using Magnifier.App.Localization;
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
        if (_handToolEnabled) await SetPanModeAsync(false);
        var resume = _relayStatus is { InputRequested: true };
        try { await StopAsync(Loc.Instance["Lens_Reason_AuxiliaryOpen"]); _resumeAfterAuxiliary = resume; }
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
        try { await StopAsync(Loc.Instance["Lens_Reason_AuxiliaryClose"]); }
        catch { resume = false; }
        ResizeLens();
        QueueGeometryUpdate();
        UpdatePointMarkers();
        UpdateControls();
        if (resume)
        {
            try { await StartInputAsync(); }
            catch (Exception exception) { PublishInputStatus(string.Format(Loc.Instance["Lens_Status_ResumeFailed_Format"], exception.Message)); }
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
            PublishInputStatus(enabled ? Loc.Instance["Lens_Status_AuxInputOn"] : Loc.Instance["Lens_Status_AuxInputOff"]);
        }
        catch (Exception exception)
        {
            SetPreviewInputToggle(false);
            PublishInputStatus(string.Format(Loc.Instance["Lens_Status_AuxInputChangeFailed_Format"], exception.Message));
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
        PublishInputStatus(string.Format(Loc.Instance["Lens_Status_PickPoint_Format"], target));
    }

    private void SelectPointFromImage(Point imagePoint)
    {
        if (!_editingAllowed || !TryMapToScreen(imagePoint, out var point, out var visualPoint)) return;
        if (_pendingPointTarget == PointTarget.A) { _pointA = point; _pointAVisual = visualPoint; }
        else { _pointB = point; _pointBVisual = visualPoint; }
        _pendingPointTarget = PointTarget.None;
        UpdateStrokeControls();
        UpdatePointMarkers();
        PublishInputStatus(string.Format(Loc.Instance["Lens_Status_PointSet_Format"], point.X, point.Y));
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
                PublishInputStatus(string.Format(Loc.Instance["Lens_Status_Countdown_Format"], remaining));
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            Hide();
            try
            {
                if (_inputSession.Begin(pointA))
                {
                    _inputSession.Complete(pointB);
                    PublishInputStatus(Loc.Instance["Lens_Status_StrokeAccepted"]);
                }
            }
            finally { if (!_closed) Show(); }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { PublishInputStatus(Loc.Instance["Lens_Status_StrokeCancelled"]); }
        catch (Exception exception)
        { PublishInputStatus(string.Format(Loc.Instance["Lens_Status_StrokeFailed_Format"], exception.Message)); }
        finally
        {
            try { _inputSession.SetInputEnabled(false); }
            catch (Exception exception) { PublishInputStatus(string.Format(Loc.Instance["Lens_Status_AuxReleaseRetry_Format"], exception.Message)); }
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
        CountdownText.Text = string.Format(Loc.Instance["Lens_Countdown_Format"], _countdownSeconds);
        PointStatusText.Text = string.Format(Loc.Instance["Lens_PointStatus_Format"], FormatPoint(_pointA), FormatPoint(_pointB));
        RunStraightStrokeButton.IsEnabled = _inputSession.IsInputEnabled && _pointA.HasValue && _pointB.HasValue
            && _straightStrokeCancellation is null && _bitmap is not null;
    }

    private static string FormatPoint(ScreenPoint? point) => point is ScreenPoint value ? $"({value.X}, {value.Y})" : Loc.Instance["Lens_PointUnset"];
    private enum PointTarget { None, A, B }
}
