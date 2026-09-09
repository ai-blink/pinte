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
    private ScreenRegion _region = new(100, 150, 380, 230);
    private ScreenRegion? _pendingRegion;
    private bool _editingEnabled = true;
    private bool _adjusting;
    private bool _settingRegion;

    public SelectionOverlayWindow(IScreenCapture capture)
    {
        InitializeComponent();
        _capture = capture;
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

    public event Action? AdjustmentStarted;

    public ScreenRegion Region => _region;

    public nint WindowHandle { get; private set; }

    public void SetRegion(ScreenRegion region)
    {
        if (!_editingEnabled || _adjusting)
        {
            return;
        }

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
                Width = Math.Max(MinWidth, region.Width / dpi.DpiScaleX + 40);
                Height = Math.Max(MinHeight, region.Height / dpi.DpiScaleY + 80);
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
            Left += e.HorizontalChange;
            Top += e.VerticalChange;
            PublishRegion();
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

        var width = ActualWidth;
        var height = ActualHeight;
        if (edge.Contains('W'))
        {
            var delta = Math.Min(e.HorizontalChange, width - MinWidth);
            Left += delta;
            Width = width - delta;
        }
        else if (edge.Contains('E'))
        {
            Width = Math.Max(MinWidth, width + e.HorizontalChange);
        }

        if (edge.Contains('N'))
        {
            var delta = Math.Min(e.VerticalChange, height - MinHeight);
            Top += delta;
            Height = height - delta;
        }
        else if (edge.Contains('S'))
        {
            Height = Math.Max(MinHeight, height + e.VerticalChange);
        }

        UpdateLayout();
        PublishRegion();
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

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ReturnRequested?.Invoke();
            e.Handled = true;
        }
    }

}
