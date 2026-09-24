using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private LensDisplayMode _displayMode;
    private bool _externalInputSuspended, _sourceEditing, _moveReady, _finishingMove, _normalToolbarNarrow;
    private double _normalToolbarWideWidth;
    private readonly SemaphoreSlim _suspensionLock = new(1, 1);

    private void Toolbar_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 컴팩트 최소 폭 484 DIP에서도 44 DIP 버튼을 유지한다. 이동 손잡이와 배율
        // 표기만 줄여 크기 조절·숨김 토글까지 한 줄에 놓는다.
        CompactReturnButton.Width = Math.Clamp(Toolbar.ActualWidth - 8 - 38 - 8 * 44 - 24, 44, 100);
        UpdateNormalToolbarDensity();
    }

    // 일반 툴바의 긴 표기가 한 줄에 안 들어가면 짧은 표기로 바꾸고 세밀 슬라이더를 숨긴다.
    // 긴 표기 폭은 긴 표기일 때만 재므로 창을 다시 넓히면 원래 표기로 돌아온다.
    private void UpdateNormalToolbarDensity()
    {
        if (NormalToolbar.Visibility != Visibility.Visible) return;
        if (!_normalToolbarNarrow)
            _normalToolbarWideWidth = NormalToolbar.Children.OfType<UIElement>().Sum(child => child.DesiredSize.Width);
        var available = Toolbar.ActualWidth - Toolbar.Padding.Left - Toolbar.Padding.Right
            - Toolbar.BorderThickness.Left - Toolbar.BorderThickness.Right;
        SetNormalToolbarNarrow(_normalToolbarWideWidth > available);
    }

    private void SetNormalToolbarNarrow(bool narrow)
    {
        if (narrow == _normalToolbarNarrow) return;
        _normalToolbarNarrow = narrow;
        SourceEditButton.Content = narrow ? "▣" : "영역 편집";
        RegionSettingsButton.Content = narrow ? "크기" : "영역 크기";
        HideLensButton.Content = narrow ? "숨김" : "렌즈 숨김";
        ReturnButton.Content = narrow ? "↩ 복귀" : "↩ 원래 화면";
        FineZoomSlider.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        ApplyPanModeUi();
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
        CompactZoomText.IsEnabled = ZoomDecreaseButton.IsEnabled;
        CompactPanModeButton.IsEnabled = PanModeButton.IsEnabled;
        CompactResizeModeButton.IsEnabled = ResizeModeButton.IsEnabled;
        CompactAppSettingsButton.IsEnabled = AppSettingsButton.IsEnabled;
        CompactHideLensButton.IsEnabled = HideLensButton.IsEnabled;
    }

    private async Task FinishMoveAsync()
    {
        if (!_isMoving || _finishingMove) return;
        _finishingMove = true;
        _moveReady = false;
        try
        {
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
