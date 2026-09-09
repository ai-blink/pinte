namespace Magnifier.Core;

/// <summary>원본과 렌즈 콘텐츠 영역을 각각 물리 화면 픽셀로 보관한다.</summary>
public sealed record LensViewport(ScreenRegion Source, ScreenRegion Destination)
{
    public bool Contains(ScreenPoint point) =>
        point.X >= Destination.X && point.Y >= Destination.Y &&
        (long)point.X < (long)Destination.X + Destination.Width &&
        (long)point.Y < (long)Destination.Y + Destination.Height;

    /// <param name="point">렌즈 위의 절대 물리 화면 좌표. DIP는 호출 경계에서 변환한다.</param>
    public ScreenPoint MapToSource(PreviewPoint point) => PreviewCoordinateMapper.MapToScreen(
        Source,
        new PreviewSize(Destination.Width, Destination.Height),
        new PreviewPoint(point.X - Destination.X, point.Y - Destination.Y));
}
