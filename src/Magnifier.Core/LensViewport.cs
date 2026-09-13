namespace Magnifier.Core;

/// <summary>원본과 렌즈 콘텐츠 영역을 각각 물리 화면 픽셀로 보관한다.</summary>
public sealed record LensViewport(ScreenRegion Source, ScreenRegion Destination)
{
    private ScreenRegion? UncroppedSource { get; init; }
    private PreviewPoint ImageOrigin { get; init; }
    private PreviewSize ImageSize { get; init; }

    /// <summary>실제 렌더링한 이미지와 보이는 viewport의 교집합. 모든 값은 물리 화면 좌표다.</summary>
    public static bool TryCreate(ScreenRegion source, PreviewPoint imageOrigin, PreviewSize imageSize,
        PreviewPoint viewportOrigin, PreviewSize viewportSize, out LensViewport viewport)
    {
        viewport = default!;
        if (source.Width <= 0 || source.Height <= 0 || !imageSize.IsValid || !viewportSize.IsValid ||
            !double.IsFinite(imageOrigin.X) || !double.IsFinite(imageOrigin.Y) ||
            !double.IsFinite(viewportOrigin.X) || !double.IsFinite(viewportOrigin.Y)) return false;
        var left = Math.Ceiling(Math.Max(imageOrigin.X, viewportOrigin.X));
        var top = Math.Ceiling(Math.Max(imageOrigin.Y, viewportOrigin.Y));
        var right = Math.Ceiling(Math.Min(imageOrigin.X + imageSize.Width, viewportOrigin.X + viewportSize.Width));
        var bottom = Math.Ceiling(Math.Min(imageOrigin.Y + imageSize.Height, viewportOrigin.Y + viewportSize.Height));
        if (right <= left || bottom <= top || left < int.MinValue || top < int.MinValue ||
            right > int.MaxValue || bottom > int.MaxValue || right - left > int.MaxValue || bottom - top > int.MaxValue)
            return false;
        var destination = new ScreenRegion((int)left, (int)top, (int)(right - left), (int)(bottom - top));
        var start = PreviewCoordinateMapper.MapToScreen(source, imageSize,
            new PreviewPoint(left - imageOrigin.X, top - imageOrigin.Y));
        var end = PreviewCoordinateMapper.MapToScreen(source, imageSize,
            new PreviewPoint(right - 1 - imageOrigin.X, bottom - 1 - imageOrigin.Y));
        viewport = new LensViewport(new ScreenRegion(start.X, start.Y, end.X - start.X + 1, end.Y - start.Y + 1), destination)
        {
            UncroppedSource = source, ImageOrigin = imageOrigin, ImageSize = imageSize
        };
        return true;
    }

    public bool Contains(ScreenPoint point) =>
        point.X >= Destination.X && point.Y >= Destination.Y &&
        (long)point.X < (long)Destination.X + Destination.Width &&
        (long)point.Y < (long)Destination.Y + Destination.Height;

    /// <param name="point">렌즈 위의 절대 물리 화면 좌표. DIP는 호출 경계에서 변환한다.</param>
    public ScreenPoint MapToSource(PreviewPoint point)
    {
        // Crop의 정수 반올림을 다시 확대하지 않는다. 0.25×·혼합 DPI에서도 원래 변환을 유지한다.
        if (UncroppedSource is { } source)
            return PreviewCoordinateMapper.MapToScreen(source, ImageSize,
                new PreviewPoint(point.X - ImageOrigin.X, point.Y - ImageOrigin.Y));
        return PreviewCoordinateMapper.MapToScreen(Source,
            new PreviewSize(Destination.Width, Destination.Height),
            new PreviewPoint(point.X - Destination.X, point.Y - Destination.Y));
    }
}
