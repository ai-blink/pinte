using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private const double ResizeGripMargin = 24;
    private const double ResizeEdgeHitThickness = 20;
    private const double ResizeTopCornerHitSize = 24;
    private const double ResizeCornerHitSize = 48;
    private bool _isResizing, _resizeReady, _resizeControlsVisible, _finishingResize;
    private int _resizeRevision;
    private Point _resizeStartPointer;
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
        // 44 DIP 손잡이는 유지하되, 절반만 창 밖에 둔다. 작은 렌즈에서
        // 조절 모드 전용 빈 여백이 화면보다 커지는 것을 막는다.
        OuterBorder.Margin = new Thickness(visible ? ResizeGripMargin : 8);
        ResizeDoneButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ApplyMinimumLensSize();
        ArrangeResizeControls();
        QueueGeometryUpdate();
    }

    private void ApplyMinimumLensSize()
    {
        var compact = _displayMode == LensDisplayMode.Compact;
        var gripSpace = _resizeControlsVisible ? ResizeGripMargin * 2 : 0;
        MinWidth = (compact ? 440 : 640) + gripSpace;
        MinHeight = (compact ? 280 : 360) + gripSpace;
        if (double.IsFinite(Width) && Width < MinWidth) Width = MinWidth;
        if (double.IsFinite(Height) && Height < MinHeight) Height = MinHeight;
    }

    private void ArrangeResizeControls()
    {
        foreach (Thumb thumb in ResizeLayer.Children)
        {
            var direction = (string)thumb.Tag;
            var corner = direction.Length == 2;
            var edgeSize = _resizeControlsVisible ? 44 : ResizeEdgeHitThickness;
            var cornerSize = _resizeControlsVisible ? 44 : direction.Contains('N') ? ResizeTopCornerHitSize : ResizeCornerHitSize;
            var size = corner ? cornerSize : edgeSize;
            thumb.Width = corner || direction is "W" or "E" || _resizeControlsVisible ? size : double.NaN;
            thumb.Height = corner || direction is "N" or "S" || _resizeControlsVisible ? size : double.NaN;
            thumb.HorizontalAlignment = direction.Contains('W') ? HorizontalAlignment.Left :
                direction.Contains('E') ? HorizontalAlignment.Right : _resizeControlsVisible ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            thumb.VerticalAlignment = direction.Contains('N') ? VerticalAlignment.Top :
                direction.Contains('S') ? VerticalAlignment.Bottom : _resizeControlsVisible ? VerticalAlignment.Center : VerticalAlignment.Stretch;
            var cornerInset = ResizeCornerHitSize / 2;
            thumb.Margin = corner || _resizeControlsVisible ? new Thickness(0) :
                direction is "N" or "S" ? new Thickness(cornerInset, 0, cornerInset, 0) : new Thickness(0, cornerInset, 0, cornerInset);
            thumb.Background = _resizeControlsVisible ? (Brush)FindResource("AppBorderBrush") : Brushes.Transparent;
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
            await ApplyInputSuspensionAsync("렌즈 크기 조절 · 실제 입력 해제");
            _resizeStartPointer = PointToScreen(Mouse.GetPosition(this));
            _resizeStartBounds = _windows.GetWindowBounds(WindowHandle);
            if (revision == _resizeRevision && _isResizing) _resizeReady = true;
        }
        catch (Exception exception)
        {
            PublishInputStatus($"렌즈 크기 조절 해제 실패: {exception.Message}");
            ((Thumb)sender).CancelDrag();
        }
    }

    private async void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isResizing || !_resizeReady) return;
        try
        {
            var pointer = PointToScreen(Mouse.GetPosition(this));
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
        catch (Exception exception)
        {
            _resizeReady = false;
            try { await StopAsync($"렌즈 크기 조절 실패 · 입력 중지: {exception.Message}"); }
            catch { }
            ((Thumb)sender).CancelDrag();
            PublishInputStatus($"렌즈 크기 조절 실패: {exception.Message}");
        }
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

    private async void ResizeDone_OnClick(object sender, RoutedEventArgs e)
    {
        await FinishResizeAsync();
        SetResizeControlsVisible(false);
        e.Handled = true;
    }
}
