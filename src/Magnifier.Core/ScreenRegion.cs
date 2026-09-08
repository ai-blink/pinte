namespace Magnifier.Core;

public readonly record struct ScreenPoint(int X, int Y);

public readonly record struct ScreenRegion
{
    public ScreenRegion(int x, int y, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "화면 영역 너비는 0보다 커야 합니다.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "화면 영역 높이는 0보다 커야 합니다.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }
}
