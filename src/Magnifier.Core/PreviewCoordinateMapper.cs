namespace Magnifier.Core;

public readonly record struct PreviewPoint(double X, double Y);

public readonly record struct PreviewSize(double Width, double Height)
{
    public bool IsValid => double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
}

public static class PreviewCoordinateMapper
{
    public static ScreenPoint MapToScreen(ScreenRegion region, PreviewSize previewSize, PreviewPoint previewPoint)
    {
        if (!previewSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(previewSize), "미리보기 크기는 유한한 양수여야 합니다.");
        }

        if (!double.IsFinite(previewPoint.X) || !double.IsFinite(previewPoint.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(previewPoint), "미리보기 좌표는 유한해야 합니다.");
        }

        return new ScreenPoint(
            region.X + MapAxis(previewPoint.X, previewSize.Width, region.Width),
            region.Y + MapAxis(previewPoint.Y, previewSize.Height, region.Height));
    }

    private static int MapAxis(double point, double previewLength, int screenLength)
    {
        var clampedPoint = Math.Clamp(point, 0, previewLength);
        var pixelOffset = (int)Math.Floor(clampedPoint / previewLength * screenLength);
        return Math.Min(pixelOffset, screenLength - 1);
    }
}
