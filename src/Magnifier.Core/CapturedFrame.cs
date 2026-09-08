namespace Magnifier.Core;

public sealed class CapturedFrame
{
    public CapturedFrame(ScreenRegion region, ReadOnlyMemory<byte> bgra32Pixels)
    {
        var expectedByteCount = checked(region.Width * region.Height * 4);
        if (bgra32Pixels.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"BGRA32 프레임은 {expectedByteCount}바이트여야 합니다.",
                nameof(bgra32Pixels));
        }

        Region = region;
        Bgra32Pixels = bgra32Pixels;
    }

    public ScreenRegion Region { get; }

    public ReadOnlyMemory<byte> Bgra32Pixels { get; }

    public int Stride => checked(Region.Width * 4);
}
