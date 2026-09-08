using System.Windows;
using System.Windows.Controls;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private void UpdatePointMarkers()
    {
        UpdatePointMarker(PointAMarker, _pointAVisual);
        UpdatePointMarker(PointBMarker, _pointBVisual);
        UpdateStraightStrokePreview();
    }

    private void UpdatePointMarker(FrameworkElement marker, PreviewPoint? point)
    {
        if (point is not PreviewPoint visualPoint
            || _currentRegion is not ScreenRegion region
            || !TryGetRenderedImageBounds(region, out var renderedBounds))
        {
            marker.Visibility = Visibility.Collapsed;
            return;
        }

        var imagePoint = MapToRenderedImage(visualPoint, renderedBounds);
        Canvas.SetLeft(marker, imagePoint.X - (marker.Width / 2));
        Canvas.SetTop(marker, imagePoint.Y - (marker.Height / 2));
        marker.Visibility = Visibility.Visible;
    }

    private void UpdateStraightStrokePreview()
    {
        if (_pointAVisual is not PreviewPoint pointA
            || _pointBVisual is not PreviewPoint pointB
            || _currentRegion is not ScreenRegion region
            || !TryGetRenderedImageBounds(region, out var renderedBounds))
        {
            StraightStrokePreview.Visibility = Visibility.Collapsed;
            return;
        }

        var start = MapToRenderedImage(pointA, renderedBounds);
        var end = MapToRenderedImage(pointB, renderedBounds);
        StraightStrokePreview.X1 = start.X;
        StraightStrokePreview.Y1 = start.Y;
        StraightStrokePreview.X2 = end.X;
        StraightStrokePreview.Y2 = end.Y;
        StraightStrokePreview.Visibility = Visibility.Visible;
    }

    private static Point MapToRenderedImage(PreviewPoint point, Rect renderedBounds) =>
        new(
            renderedBounds.X + (point.X * renderedBounds.Width),
            renderedBounds.Y + (point.Y * renderedBounds.Height));
}
