namespace Magnifier.Core;

/// <summary>원본 물리 픽셀 영역의 크기·비율 변경을 창 UI와 분리해 계산한다.</summary>
public static class ScreenRegionSizing
{
    public static ScreenRegion Resize(
        ScreenRegion current,
        RegionResizeHandle handle,
        int horizontalChange,
        int verticalChange,
        ScreenRegion bounds,
        int minimumWidth = 80,
        int minimumHeight = 60,
        double? lockedAspectRatio = null)
    {
        if (minimumWidth <= 0) throw new ArgumentOutOfRangeException(nameof(minimumWidth));
        if (minimumHeight <= 0) throw new ArgumentOutOfRangeException(nameof(minimumHeight));
        if (lockedAspectRatio is { } requestedRatio && (!double.IsFinite(requestedRatio) || requestedRatio <= 0))
            throw new ArgumentOutOfRangeException(nameof(lockedAspectRatio));

        var west = handle is RegionResizeHandle.NorthWest or RegionResizeHandle.West or RegionResizeHandle.SouthWest;
        var east = handle is RegionResizeHandle.NorthEast or RegionResizeHandle.East or RegionResizeHandle.SouthEast;
        var north = handle is RegionResizeHandle.NorthWest or RegionResizeHandle.North or RegionResizeHandle.NorthEast;
        var south = handle is RegionResizeHandle.SouthWest or RegionResizeHandle.South or RegionResizeHandle.SouthEast;

        var requestedWidth = current.Width + (west ? -horizontalChange : east ? horizontalChange : 0);
        var requestedHeight = current.Height + (north ? -verticalChange : south ? verticalChange : 0);
        var right = current.X + current.Width;
        var bottom = current.Y + current.Height;

        if (lockedAspectRatio is { } aspect)
        {
            var widthFromHeight = (int)Math.Round(requestedHeight * aspect, MidpointRounding.AwayFromZero);
            var heightFromWidth = (int)Math.Round(requestedWidth / aspect, MidpointRounding.AwayFromZero);
            if (west || east)
            {
                requestedHeight = heightFromWidth;
            }
            else
            {
                requestedWidth = widthFromHeight;
            }
        }

        var maximumWidth = west ? right - bounds.X : bounds.X + bounds.Width - current.X;
        var maximumHeight = north ? bottom - bounds.Y : bounds.Y + bounds.Height - current.Y;
        if (lockedAspectRatio is { } locked)
        {
            maximumWidth = Math.Min(maximumWidth, (int)Math.Floor(maximumHeight * locked));
            var minimum = Math.Max(minimumWidth, (int)Math.Ceiling(minimumHeight * locked));
            requestedWidth = Math.Clamp(requestedWidth, Math.Min(minimum, maximumWidth), maximumWidth);
            requestedHeight = Math.Max(1, (int)Math.Round(requestedWidth / locked, MidpointRounding.AwayFromZero));
        }
        else
        {
            requestedWidth = Math.Clamp(requestedWidth, Math.Min(minimumWidth, maximumWidth), maximumWidth);
            requestedHeight = Math.Clamp(requestedHeight, Math.Min(minimumHeight, maximumHeight), maximumHeight);
        }

        var x = west ? right - requestedWidth : current.X;
        var y = north ? bottom - requestedHeight : current.Y;
        if (lockedAspectRatio is not null && (west || east) && !(north || south))
        {
            y = current.Y + (current.Height - requestedHeight) / 2;
        }
        else if (lockedAspectRatio is not null && (north || south) && !(west || east))
        {
            x = current.X + (current.Width - requestedWidth) / 2;
        }

        x = Math.Clamp(x, bounds.X, bounds.X + bounds.Width - requestedWidth);
        y = Math.Clamp(y, bounds.Y, bounds.Y + bounds.Height - requestedHeight);
        return new ScreenRegion(x, y, requestedWidth, requestedHeight);
    }

    public static ScreenRegion Fit(ScreenRegion requested, ScreenRegion bounds, int minimumWidth = 80, int minimumHeight = 60)
    {
        var width = Math.Clamp(requested.Width, Math.Min(minimumWidth, bounds.Width), bounds.Width);
        var height = Math.Clamp(requested.Height, Math.Min(minimumHeight, bounds.Height), bounds.Height);
        return new ScreenRegion(
            Math.Clamp(requested.X, bounds.X, bounds.X + bounds.Width - width),
            Math.Clamp(requested.Y, bounds.Y, bounds.Y + bounds.Height - height),
            width,
            height);
    }
}

public enum RegionResizeHandle
{
    NorthWest,
    North,
    NorthEast,
    West,
    East,
    SouthWest,
    South,
    SouthEast
}
