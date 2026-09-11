using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionOverlayWindow : Window
{
    private readonly IScreenCapture _capture;
    private readonly IWindowEnvironment _windows;
    private ScreenRegion _region = new(100, 150, 380, 230);
    private ScreenRegion? _pendingRegion;
    private bool _editingEnabled = true;
    private bool _adjusting;
    private bool _settingRegion;
    private double? _lockedAspectRatio;

    public SelectionOverlayWindow(IScreenCapture capture, IWindowEnvironment windows)
    {
        InitializeComponent();
        _capture = capture;
        _windows = windows;
        SourceInitialized += (_, _) =>
        {
            WindowHandle = new WindowInteropHelper(this).Handle;
            _capture.SetWindowCaptureExclusion(WindowHandle, true);
        };
        Loaded += (_, _) =>
        {
            if (_pendingRegion is ScreenRegion pending)
            {
                _pendingRegion = null;
                SetRegion(pending);
            }
            else
            {
                PublishRegion();
            }
        };
        LocationChanged += (_, _) => PublishRegion();
        SizeChanged += (_, _) => PublishRegion();
        ContentAperture.LayoutUpdated += (_, _) => PublishRegion();
        Closing += (_, _) => ReturnRequested?.Invoke();
    }

    public event Action<ScreenRegion>? RegionChanged;

    public event Action? ReturnRequested;

    public event Action? RegionConfirmed;

    public event Action? AdjustmentStarted;

    public event Action? RegionSettingsRequested;

    public event Action? AppSettingsRequested;

    public ScreenRegion Region => _region;

    public double? LockedAspectRatio => _lockedAspectRatio;

    public nint WindowHandle { get; private set; }

    public void SetSelectionMode(bool selecting)
    {
        ConfirmRegionButton.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;
        ConfirmRegionButton.IsEnabled = selecting;
        Title = selecting ? "화면 영역 지정 · 테두리를 맞춘 뒤 이 영역 확대" : "확대할 원본 영역";
    }

    public void SetRegion(ScreenRegion region) => ApplyRegion(region, force: false);

    public void ApplySizing(int width, int height, double? lockedAspectRatio)
    {
        if (width <= 0 || height <= 0) return;
        _lockedAspectRatio = lockedAspectRatio is { } ratio && double.IsFinite(ratio) && ratio > 0 ? ratio : null;
        ApplyRegion(new ScreenRegion(_region.X, _region.Y, width, height), force: true);
    }

    private void ApplyRegion(ScreenRegion region, bool force)
    {
        if ((!_editingEnabled || _adjusting) && !force)
        {
            return;
        }

        region = ScreenRegionSizing.Fit(region, _windows.DesktopBounds);

        if (!IsLoaded)
        {
            _region = region;
            _pendingRegion = region;
            return;
        }

        _settingRegion = true;
        try
        {
            // PointToScreen is physical pixels; Width/Left are WPF DIPs. Measuring the
            // current aperture avoids treating a negative monitor origin as a DIP origin.
            // A second pass accounts for a DPI transition triggered by the first move.
            for (var pass = 0; pass < 2; pass++)
            {
                UpdateLayout();
                var dpi = VisualTreeHelper.GetDpi(this);
                var topLeft = ContentAperture.PointToScreen(new Point());
                Left += (region.X - topLeft.X) / dpi.DpiScaleX;
                Top += (region.Y - topLeft.Y) / dpi.DpiScaleY;
                Width = Math.Max(MinWidth, region.Width / dpi.DpiScaleX + 56);
                Height = Math.Max(MinHeight, region.Height / dpi.DpiScaleY + 108);
            }
            UpdateLayout();
        }
        finally
        {
            _settingRegion = false;
        }

        PublishRegion();
    }

    public void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        Thumb[] handles = [MoveHandle, NorthWestHandle, NorthHandle, NorthEastHandle,
            WestHandle, EastHandle, SouthWestHandle, SouthHandle, SouthEastHandle];
        foreach (var handle in handles)
        {
            handle.IsEnabled = enabled;
        }
        if (!enabled && Mouse.Captured is Thumb thumb && Window.GetWindow(thumb) == this)
        {
            thumb.CancelDrag();
        }
    }

    private void Handle_OnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (!_editingEnabled)
        {
            ((Thumb)sender).CancelDrag();
            e.Handled = true;
            return;
        }

        AdjustmentStarted?.Invoke();
        _adjusting = _editingEnabled;
        if (!_adjusting)
        {
            ((Thumb)sender).CancelDrag();
        }
        e.Handled = true;
    }

    private void MoveHandle_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_editingEnabled && _adjusting)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var region = ScreenRegionSizing.Fit(new ScreenRegion(
                _region.X + (int)Math.Round(e.HorizontalChange * dpi.DpiScaleX),
                _region.Y + (int)Math.Round(e.VerticalChange * dpi.DpiScaleY),
                _region.Width, _region.Height), _windows.DesktopBounds);
            ApplyRegion(region, force: true);
        }
        e.Handled = true;
    }

    private void ResizeHandle_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_editingEnabled || !_adjusting || ((Thumb)sender).Tag is not string edge)
        {
            e.Handled = true;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var handle = edge switch
        {
            "NW" => RegionResizeHandle.NorthWest,
            "N" => RegionResizeHandle.North,
            "NE" => RegionResizeHandle.NorthEast,
            "W" => RegionResizeHandle.West,
            "E" => RegionResizeHandle.East,
            "SW" => RegionResizeHandle.SouthWest,
            "S" => RegionResizeHandle.South,
            _ => RegionResizeHandle.SouthEast
        };
        var region = ScreenRegionSizing.Resize(_region, handle,
            (int)Math.Round(e.HorizontalChange * dpi.DpiScaleX),
            (int)Math.Round(e.VerticalChange * dpi.DpiScaleY),
            _windows.DesktopBounds, lockedAspectRatio: _lockedAspectRatio);
        ApplyRegion(region, force: true);
        e.Handled = true;
    }

    private void Handle_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _adjusting = false;
        PublishRegion();
        e.Handled = true;
    }

    private void PublishRegion()
    {
        if (!IsLoaded || _settingRegion || ContentAperture.ActualWidth <= 0 || ContentAperture.ActualHeight <= 0)
        {
            return;
        }

        var topLeft = ContentAperture.PointToScreen(new Point());
        var bottomRight = ContentAperture.PointToScreen(
            new Point(ContentAperture.ActualWidth, ContentAperture.ActualHeight));
        var left = (int)Math.Round(topLeft.X);
        var top = (int)Math.Round(topLeft.Y);
        var region = new ScreenRegion(left, top,
            Math.Max(1, (int)Math.Round(bottomRight.X) - left),
            Math.Max(1, (int)Math.Round(bottomRight.Y) - top));
        if (region == _region)
        {
            return;
        }

        _region = region;
        RegionChanged?.Invoke(region);
    }

    private void ReturnButton_OnClick(object sender, RoutedEventArgs e)
    {
        ReturnRequested?.Invoke();
        e.Handled = true;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        ReturnRequested?.Invoke();
        e.Handled = true;
    }

    private void ConfirmRegionButton_OnClick(object sender, RoutedEventArgs e)
    {
        ConfirmRegionButton.IsEnabled = false;
        RegionConfirmed?.Invoke();
        e.Handled = true;
    }

    private void RegionSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        RegionSettingsRequested?.Invoke();
        e.Handled = true;
    }

    private void AppSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        AppSettingsRequested?.Invoke();
        e.Handled = true;
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ReturnRequested?.Invoke();
            e.Handled = true;
        }
    }

}
