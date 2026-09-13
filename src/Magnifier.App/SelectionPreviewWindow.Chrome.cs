using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private LensDisplayMode _displayMode;
    private bool _externalInputSuspended, _sourceEditing, _moveReady, _finishingMove;
    private readonly SemaphoreSlim _suspensionLock = new(1, 1);

    private void Toolbar_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        CompactReturnButton.Width = Math.Clamp(Toolbar.ActualWidth - 8 - 7 * 44 - 48, 56, 100);
    }

    public void SetDisplayMode(LensDisplayMode mode)
    {
        _displayMode = mode;
        var compact = mode == LensDisplayMode.Compact;
        if (compact)
        {
            _resumeAfterAuxiliary = false;
            AuxiliaryTools.IsExpanded = false;
        }
        TitleRow.Height = new GridLength(compact ? 0 : 52);
        TitleBar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        NormalToolbar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactToolbar.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        AuxiliaryTools.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ImageDescription.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        Toolbar.Height = compact ? 48 : double.NaN;
        Toolbar.Margin = compact ? new Thickness(0) : new Thickness(8, 7, 8, 5);
        Toolbar.Padding = compact ? new Thickness(4, 2, 4, 2) : new Thickness(4);
        Toolbar.BorderThickness = compact ? new Thickness(0) : new Thickness(1);
        Toolbar.CornerRadius = new CornerRadius(compact ? 0 : 10);
        ImageBorder.Margin = compact ? new Thickness(0) : new Thickness(8, 0, 8, 0);
        ImageBorder.BorderThickness = new Thickness(compact ? 0 : 1);
        ImageBorder.CornerRadius = new CornerRadius(compact ? 0 : 10);
        OuterBorder.CornerRadius = new CornerRadius(compact ? 4 : 14);
        OuterBorder.Effect = compact ? null : new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 24, ShadowDepth = 8, Opacity = 0.18, Color = Color.FromRgb(23, 35, 47) };
        ApplyMinimumLensSize();
        ApplyPanModeUi();
        UpdatePanBars();
        QueueGeometryUpdate();
    }

    public void SetSourceEditing(bool editing)
    {
        _sourceEditing = editing;
        if (editing)
        {
            _inputRequestRevision++;
            _resumeAfterAuxiliary = false;
            CancelStraightStroke();
            _inputSession.SetInputEnabled(false);
            SetPreviewInputToggle(false);
        }
        UpdateControls();
    }

    public void ShowSourceIndicatorFailure(string? reason)
    {
        SourceIndicatorErrorText.Text = reason is null ? string.Empty : $"원본 윤곽선 숨김 · {reason}";
        SourceIndicatorErrorText.ToolTip = SourceIndicatorErrorText.Text;
        SourceIndicatorErrorText.Visibility = reason is null ? Visibility.Collapsed : Visibility.Visible;
        QueueGeometryUpdate();
    }

    private void SourceEdit_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_editingAllowed || _sourceEditing) return;
        e.Handled = true;
        // Main이 먼저 외부 보류를 건 뒤 손 도구를 종료하므로 중간 재개가 없다.
        SourceEditRequested?.Invoke();
    }

    private async Task ApplyInputSuspensionAsync(string reason)
    {
        await _suspensionLock.WaitAsync();
        try
        {
            await _relay.SetSuspendedAsync(_externalInputSuspended || _handToolEnabled || _isResizing || _isMoving, reason);
        }
        finally { _suspensionLock.Release(); }
    }

    private void UpdateCompactControls()
    {
        CompactMoveThumb.IsEnabled = TitleThumb.IsEnabled;
        CompactSourceEditButton.IsEnabled = SourceEditButton.IsEnabled;
        CompactZoomDecreaseButton.IsEnabled = ZoomDecreaseButton.IsEnabled;
        CompactZoomIncreaseButton.IsEnabled = ZoomIncreaseButton.IsEnabled;
        CompactPanModeButton.IsEnabled = PanModeButton.IsEnabled;
        CompactAppSettingsButton.IsEnabled = AppSettingsButton.IsEnabled;
    }

    private async Task FinishMoveAsync()
    {
        if (!_isMoving || _finishingMove) return;
        _finishingMove = true;
        _moveReady = false;
        try
        {
            KeepLensOnScreen();
            await RefreshGeometryAsync();
            _isMoving = false;
            await ApplyInputSuspensionAsync("렌즈 이동 완료 · 최신 화면과 버튼 해제 대기");
        }
        catch (Exception exception)
        {
            try { await StopAsync($"렌즈 이동 실패 · 입력 중지: {exception.Message}"); }
            catch { }
            PublishInputStatus($"렌즈 이동 해제 실패: {exception.Message}");
        }
        finally { _finishingMove = false; }
    }
}
