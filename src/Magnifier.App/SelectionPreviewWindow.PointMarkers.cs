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
        if (!AuxiliaryTools.IsExpanded || point is not PreviewPoint visualPoint
            || _currentRegion is not ScreenRegion region
            || !TryGetRenderedImageBounds(region, out var renderedBounds))
        {
            marker.Visibility = Visibility.Collapsed;
            return;
        }

        var imagePoint = MapToMarkerLayer(visualPoint, renderedBounds);
        Canvas.SetLeft(marker, imagePoint.X - (marker.Width / 2));
        Canvas.SetTop(marker, imagePoint.Y - (marker.Height / 2));
        marker.Visibility = Visibility.Visible;
    }

    private void UpdateStraightStrokePreview()
    {
        if (!AuxiliaryTools.IsExpanded || _pointAVisual is not PreviewPoint pointA
            || _pointBVisual is not PreviewPoint pointB
            || _currentRegion is not ScreenRegion region
            || !TryGetRenderedImageBounds(region, out var renderedBounds))
        {
            StraightStrokePreview.Visibility = Visibility.Collapsed;
            return;
        }

        var start = MapToMarkerLayer(pointA, renderedBounds);
        var end = MapToMarkerLayer(pointB, renderedBounds);
        StraightStrokePreview.X1 = start.X;
        StraightStrokePreview.Y1 = start.Y;
        StraightStrokePreview.X2 = end.X;
        StraightStrokePreview.Y2 = end.Y;
        StraightStrokePreview.Visibility = Visibility.Visible;
    }

    private Point MapToMarkerLayer(PreviewPoint point, Rect renderedBounds)
    {
        var imagePoint = new Point(
            renderedBounds.X + (point.X * renderedBounds.Width),
            renderedBounds.Y + (point.Y * renderedBounds.Height));
        return CapturedImage.TranslatePoint(imagePoint, PointMarkerLayer);
    }
}
