using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private const double ResizeGripMargin = 12;
    private const double ResizeVisibleHandleSize = 12;
    // 완료 버튼(36)이 렌즈 콘텐츠를 가리지 않도록 조절 모드에서만 아래에 띠를 둔다.
    private const double ResizeDoneStripHeight = 42;
    private bool _isResizing, _resizeReady, _resizeControlsVisible, _finishingResize;
    private int _resizeRevision;
    private Point _resizeStartPointer;
    private Point _resizeLatestPointer;
    private ScreenRegion _resizeStartBounds;
    private string _resizeDirection = string.Empty;

    private void InitializeResizeControls()
    {
        foreach (var (direction, cursor, name) in new[]
        {
            ("N", Cursors.SizeNS, "위"), ("S", Cursors.SizeNS, "아래"),
            ("W", Cursors.SizeWE, "왼쪽"), ("E", Cursors.SizeWE, "오른쪽"),
            ("NW", Cursors.SizeNWSE, "왼쪽 위"), ("NE", Cursors.SizeNESW, "오른쪽 위"),
            ("SW", Cursors.SizeNESW, "왼쪽 아래"), ("SE", Cursors.SizeNWSE, "오른쪽 아래")
        })
        {
            var thumb = new Thumb { Tag = direction, Cursor = cursor, Focusable = false,
                Background = Brushes.Transparent, ToolTip = $"렌즈 {name} 크기 조절" };
            System.Windows.Automation.AutomationProperties.SetName(thumb, $"렌즈 {name} 크기 조절");
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
                { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = border };
            thumb.DragStarted += ResizeThumb_OnDragStarted;
            thumb.DragDelta += ResizeThumb_OnDragDelta;
            thumb.DragCompleted += ResizeThumb_OnDragCompleted;
            thumb.LostMouseCapture += ResizeThumb_OnLostMouseCapture;
            ResizeLayer.Children.Add(thumb);
        }
        ArrangeResizeControls();
    }

    public void SetResizeControlsVisible(bool visible)
    {
        _resizeControlsVisible = visible;
        // 마우스 조절 모드는 작은 12 DIP 손잡이와 얇은 여백만 사용한다.
        // 렌즈 콘텐츠와 최소 창 크기가 조절 UI 때문에 크게 밀리지 않아야 한다.
        OuterBorder.Margin = visible
            ? new Thickness(ResizeGripMargin, ResizeGripMargin, ResizeGripMargin, ResizeGripMargin + ResizeDoneStripHeight)
            : new Thickness(8);
        ResizeDoneButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ApplyResizeModeUi();
        ApplyMinimumLensSize();
        ArrangeResizeControls();
        QueueGeometryUpdate();
    }

    private void ApplyMinimumLensSize()
    {
        var compact = _displayMode == LensDisplayMode.Compact;
        var gripSpace = _resizeControlsVisible ? ResizeGripMargin * 2 : 0;
        MinWidth = (compact ? 484 : 640) + gripSpace;
        MinHeight = (compact ? 280 : 360) + gripSpace + (_resizeControlsVisible ? ResizeDoneStripHeight : 0);
        if (double.IsFinite(Width) && Width < MinWidth) Width = MinWidth;
        if (double.IsFinite(Height) && Height < MinHeight) Height = MinHeight;
    }

    private void ArrangeResizeControls()
    {
        foreach (Thumb thumb in ResizeLayer.Children)
        {
            // 조절 모드가 꺼져 있으면 가장자리 판정 영역을 두지 않는다. 판정 폭이
            // 테두리 여백을 넘어 확대 화면과 겹치면 relay가 가상 커서로 가져간다.
            var direction = (string)thumb.Tag;
            thumb.Visibility = _resizeControlsVisible ? Visibility.Visible : Visibility.Collapsed;
            thumb.Width = ResizeVisibleHandleSize;
            thumb.Height = ResizeVisibleHandleSize;
            thumb.HorizontalAlignment = direction.Contains('W') ? HorizontalAlignment.Left :
                direction.Contains('E') ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            thumb.VerticalAlignment = direction.Contains('N') ? VerticalAlignment.Top :
                direction.Contains('S') ? VerticalAlignment.Bottom : VerticalAlignment.Center;
            thumb.Background = (Brush)FindResource("AppBorderBrush");
        }
    }

    private void UpdateResizeControlState()
    {
        foreach (Thumb thumb in ResizeLayer.Children)
            thumb.IsEnabled = !_sourceEditing && !IsInteractionLocked && (_isResizing || _editingAllowed);
    }

    private async void ResizeThumb_OnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (!_editingAllowed || _sourceEditing || WindowHandle == 0) { ((Thumb)sender).CancelDrag(); return; }
        _isResizing = true;
        _resizeReady = false;
        var revision = ++_resizeRevision;
        _resizeDirection = (string)((Thumb)sender).Tag;
        try
        {
            // DragStarted와 첫 DragDelta 사이에 await가 끼면 짧은 실제 드래그의
            // 유일한 이동이 버려질 수 있다. 물리 시작 좌표와 현재 좌표를 먼저 고정한다.
            _resizeStartPointer = PointToScreen(Mouse.GetPosition(this));
            _resizeLatestPointer = _resizeStartPointer;
            _resizeStartBounds = _windows.GetWindowBounds(WindowHandle);
            await ApplyInputSuspensionAsync("렌즈 크기 조절 · 실제 입력 해제");
            if (revision == _resizeRevision && _isResizing)
            {
                _resizeReady = true;
                ApplyResizeAt(_resizeLatestPointer);
            }
        }
        catch (Exception exception)
        {
            PublishInputStatus($"렌즈 크기 조절 해제 실패: {exception.Message}");
            ((Thumb)sender).CancelDrag();
        }
    }

    private async void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isResizing) return;
        _resizeLatestPointer = PointToScreen(Mouse.GetPosition(this));
        if (!_resizeReady) return;
        try
        {
            ApplyResizeAt(_resizeLatestPointer);
        }
        catch (Exception exception)
        {
            _resizeReady = false;
            try { await StopAsync($"렌즈 크기 조절 실패 · 입력 중지: {exception.Message}"); }
            catch { }
            ((Thumb)sender).CancelDrag();
            PublishInputStatus($"렌즈 크기 조절 실패: {exception.Message}");
        }
    }

    private void ApplyResizeAt(Point pointer)
    {
        var dx = (int)Math.Round(pointer.X - _resizeStartPointer.X);
        var dy = (int)Math.Round(pointer.Y - _resizeStartPointer.Y);
        var dpi = VisualTreeHelper.GetDpi(this);
        var minWidth = (int)Math.Ceiling(MinWidth * dpi.DpiScaleX);
        var minHeight = (int)Math.Ceiling(MinHeight * dpi.DpiScaleY);
        var left = _resizeStartBounds.X;
        var top = _resizeStartBounds.Y;
        var right = left + _resizeStartBounds.Width;
        var bottom = top + _resizeStartBounds.Height;
        if (_resizeDirection.Contains('W')) left = Math.Min(left + dx, right - minWidth);
        if (_resizeDirection.Contains('E')) right = Math.Max(right + dx, left + minWidth);
        if (_resizeDirection.Contains('N')) top = Math.Min(top + dy, bottom - minHeight);
        if (_resizeDirection.Contains('S')) bottom = Math.Max(bottom + dy, top + minHeight);
        _windows.PlaceWindow(WindowHandle, new ScreenRegion(left, top, right - left, bottom - top));
        QueueGeometryUpdate();
    }

    private async void ResizeThumb_OnDragCompleted(object sender, DragCompletedEventArgs e) => await FinishResizeAsync();
    private async void ResizeThumb_OnLostMouseCapture(object sender, MouseEventArgs e) => await FinishResizeAsync();

    private async Task FinishResizeAsync()
    {
        if (!_isResizing || _finishingResize) return;
        _finishingResize = true;
        _resizeReady = false;
        ++_resizeRevision;
        try
        {
            await RefreshGeometryAsync();
            _isResizing = false;
            await ApplyInputSuspensionAsync("렌즈 크기 확정 · 최신 화면과 버튼 해제 대기");
        }
        catch (Exception exception)
        {
            try { await StopAsync($"렌즈 크기 확정 실패 · 입력 중지: {exception.Message}"); }
            catch { }
            PublishInputStatus($"렌즈 크기 확정 실패: {exception.Message}");
        }
        finally { _finishingResize = false; UpdateControls(); }
    }

    private void ApplyResizeModeUi()
    {
        var tip = _resizeControlsVisible
            ? "크기 조절 켜짐: 가장자리 손잡이를 끌어 크기를 바꿉니다. 누르면 끕니다"
            : "크기 조절 꺼짐: 켜면 렌즈 가장자리에 손잡이가 나타납니다";
        var name = _resizeControlsVisible ? "렌즈 크기 조절 손잡이 켜짐, 누르면 끔" : "렌즈 크기 조절 손잡이 꺼짐, 누르면 켬";
        ResizeModeButton.Style = (Style)FindResource(_resizeControlsVisible ? "AccentButtonStyle" : "IconButtonStyle");
        CompactResizeModeButton.Style = (Style)FindResource(_resizeControlsVisible ? "CompactActiveButtonStyle" : "CompactButtonStyle");
        foreach (var button in new[] { ResizeModeButton, CompactResizeModeButton })
        {
            button.ToolTip = tip;
            System.Windows.Automation.AutomationProperties.SetName(button, name);
        }
    }

    private async void ResizeMode_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_resizeControlsVisible) await FinishResizeAsync();
        SetResizeControlsVisible(!_resizeControlsVisible);
    }

    private async void ResizeDone_OnClick(object sender, RoutedEventArgs e)
    {
        await FinishResizeAsync();
        SetResizeControlsVisible(false);
        e.Handled = true;
    }
}
