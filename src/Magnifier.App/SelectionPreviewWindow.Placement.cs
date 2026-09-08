using System.Windows;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow
{
    private const double CaptureGap = 16;

    public bool OverlapsCaptureRegion(ScreenRegion region)
    {
        if (!IsVisible || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        var topLeft = PointToScreen(new Point(0, 0));
        var bottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
        var previewBounds = new Rect(topLeft, bottomRight);
        var captureBounds = new Rect(region.X, region.Y, region.Width, region.Height);
        return previewBounds.IntersectsWith(captureBounds);
    }

    public void PlaceOutsideCaptureRegion(ScreenRegion region)
    {
        var previewWidth = ActualWidth > 0 ? ActualWidth : Width;
        var previewHeight = ActualHeight > 0 ? ActualHeight : Height;
        var virtualBounds = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var captureBounds = new Rect(region.X, region.Y, region.Width, region.Height);
        var alignedLeft = Clamp(region.X, virtualBounds.Left, virtualBounds.Right - previewWidth);
        var alignedTop = Clamp(region.Y, virtualBounds.Top, virtualBounds.Bottom - previewHeight);
        var candidates = new[]
        {
            new Point(region.X - previewWidth - CaptureGap, alignedTop),
            new Point(region.X + region.Width + CaptureGap, alignedTop),
            new Point(alignedLeft, region.Y - previewHeight - CaptureGap),
            new Point(alignedLeft, region.Y + region.Height + CaptureGap)
        };

        foreach (var candidate in candidates)
        {
            var candidateBounds = new Rect(candidate.X, candidate.Y, previewWidth, previewHeight);
            if (virtualBounds.Contains(candidateBounds) && !candidateBounds.IntersectsWith(captureBounds))
            {
                Left = candidate.X;
                Top = candidate.Y;
                return;
            }
        }

        var fallbackCandidates = new[]
        {
            new Point(virtualBounds.Left, virtualBounds.Top),
            new Point(virtualBounds.Right - previewWidth, virtualBounds.Top),
            new Point(virtualBounds.Left, virtualBounds.Bottom - previewHeight),
            new Point(virtualBounds.Right - previewWidth, virtualBounds.Bottom - previewHeight)
        };
        var fallback = fallbackCandidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Overlap = IntersectionArea(
                    new Rect(candidate.X, candidate.Y, previewWidth, previewHeight),
                    captureBounds)
            })
            .OrderBy(candidate => candidate.Overlap)
            .First();

        Left = fallback.Candidate.X;
        Top = fallback.Candidate.Y;
    }

    public void ShowCaptureOverlap()
    {
        CaptureStatusText.Text = "미리보기 창이 원본 영역과 겹쳐 화면 갱신을 잠시 멈췄습니다";
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);
    }

    private static double IntersectionArea(Rect first, Rect second)
    {
        var intersection = Rect.Intersect(first, second);
        return intersection.IsEmpty ? 0 : intersection.Width * intersection.Height;
    }
}
