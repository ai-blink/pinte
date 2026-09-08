namespace Magnifier.Core;

public interface IScreenCapture
{
    CapturedFrame Capture(ScreenRegion region);

    void SetWindowCaptureExclusion(nint windowHandle, bool excludeFromCapture);
}
