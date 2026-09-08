namespace Magnifier.Core;

public interface IScreenCapture
{
    CapturedFrame Capture(ScreenRegion region);
}
