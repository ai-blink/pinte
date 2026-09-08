using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionOverlayWindow : Window
{
    private const double MinimumSelectionLength = 16;
    private Point? _selectionStart;

    public SelectionOverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => CoverVirtualScreen();
        Closed += (_, _) => ReleaseSelectionCapture();
        Deactivated += (_, _) => CancelActiveSelection();
    }

    public ScreenRegion? SelectedRegion { get; private set; }

    private void CoverVirtualScreen()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Activate();
        Focus();
    }

    private void OverlaySurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _selectionStart = e.GetPosition(OverlaySurface);
        SelectionRectangle.Visibility = Visibility.Visible;
        UpdateSelectionRectangle(_selectionStart.Value, _selectionStart.Value);
        Mouse.Capture(OverlaySurface);
        e.Handled = true;
    }

    private void OverlaySurface_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionStart is not Point selectionStart || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelectionRectangle(selectionStart, e.GetPosition(OverlaySurface));
    }

    private void OverlaySurface_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionStart is not Point selectionStart)
        {
            return;
        }

        var selection = CreateSelectionRect(selectionStart, e.GetPosition(OverlaySurface));
        ReleaseSelectionCapture();
        _selectionStart = null;

        if (selection.Width < MinimumSelectionLength || selection.Height < MinimumSelectionLength)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            HintText.Text = "영역의 너비와 높이를 조금 더 크게 드래그하세요";
            return;
        }

        SelectedRegion = CreateScreenRegion(selection);
        DialogResult = true;
    }

    private void OverlaySurface_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        CancelActiveSelection();
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CancelActiveSelection();
        Close();
    }

    private void UpdateSelectionRectangle(Point start, Point current)
    {
        var selection = CreateSelectionRect(start, current);
        Canvas.SetLeft(SelectionRectangle, selection.Left);
        Canvas.SetTop(SelectionRectangle, selection.Top);
        SelectionRectangle.Width = selection.Width;
        SelectionRectangle.Height = selection.Height;
    }

    private static Rect CreateSelectionRect(Point start, Point current)
    {
        return new Rect(
            new Point(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y)),
            new Point(Math.Max(start.X, current.X), Math.Max(start.Y, current.Y)));
    }

    private ScreenRegion CreateScreenRegion(Rect selection)
    {
        var topLeft = OverlaySurface.PointToScreen(selection.TopLeft);
        var bottomRight = OverlaySurface.PointToScreen(selection.BottomRight);
        var left = (int)Math.Floor(Math.Min(topLeft.X, bottomRight.X));
        var top = (int)Math.Floor(Math.Min(topLeft.Y, bottomRight.Y));
        var right = (int)Math.Ceiling(Math.Max(topLeft.X, bottomRight.X));
        var bottom = (int)Math.Ceiling(Math.Max(topLeft.Y, bottomRight.Y));

        return new ScreenRegion(left, top, right - left, bottom - top);
    }

    private void CancelActiveSelection()
    {
        if (_selectionStart is null)
        {
            return;
        }

        _selectionStart = null;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        ReleaseSelectionCapture();
    }

    private void ReleaseSelectionCapture()
    {
        if (Mouse.Captured == OverlaySurface)
        {
            Mouse.Capture(null);
        }
    }
}
