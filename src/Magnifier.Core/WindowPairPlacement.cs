namespace Magnifier.Core;

/// <summary>두 창의 전체 물리 크기를 사용해 제목부까지 작업 영역 안에 배치한다.</summary>
public static class WindowPairPlacement
{
    public static (ScreenRegion Frame, ScreenRegion Lens) Lower(
        ScreenRegion work, ScreenRegion frame, ScreenRegion lens)
    {
        var fw = Math.Min(frame.Width, work.Width);
        var fh = Math.Min(frame.Height, work.Height);
        var lw = Math.Min(lens.Width, work.Width);
        var lh = Math.Min(lens.Height, work.Height);
        var gap = Math.Min(32, Math.Min(work.Width, work.Height) / 20);
        if (fw + gap + lw <= work.Width)
        {
            var left = work.X + (work.Width - fw - gap - lw) / 2;
            var top = work.Y + (int)((work.Height - Math.Max(fh, lh)) * 0.8);
            return (new(left, top, fw, fh), new(left + fw + gap, top, lw, lh));
        }

        // Narrow displays stack the windows when space permits, always within the work area.
        var stacked = fh + gap + lh;
        var frameTop = work.Y + (int)(Math.Max(0, work.Height - stacked) * 0.8);
        var lensTop = Math.Min(frameTop + fh + gap, work.Y + work.Height - lh);
        return (new(work.X + (work.Width - fw) / 2, frameTop, fw, fh),
            new(work.X + (work.Width - lw) / 2, lensTop, lw, lh));
    }
}
