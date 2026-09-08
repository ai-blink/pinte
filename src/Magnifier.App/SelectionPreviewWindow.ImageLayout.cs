using System.Windows;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private bool TryGetRenderedImageBounds(ScreenRegion region, out Rect renderedBounds)
    {
        if (CapturedImage.ActualWidth <= 0 || CapturedImage.ActualHeight <= 0)
        {
            renderedBounds = default;
            return false;
        }

        var regionRatio = (double)region.Width / region.Height;
        var imageRatio = CapturedImage.ActualWidth / CapturedImage.ActualHeight;
        var renderedWidth = CapturedImage.ActualWidth;
        var renderedHeight = CapturedImage.ActualHeight;
        var offsetX = 0d;
        var offsetY = 0d;

        if (imageRatio > regionRatio)
        {
            renderedWidth = renderedHeight * regionRatio;
            offsetX = (CapturedImage.ActualWidth - renderedWidth) / 2;
        }
        else
        {
            renderedHeight = renderedWidth / regionRatio;
            offsetY = (CapturedImage.ActualHeight - renderedHeight) / 2;
        }

        renderedBounds = new Rect(offsetX, offsetY, renderedWidth, renderedHeight);
        return true;
    }
}
