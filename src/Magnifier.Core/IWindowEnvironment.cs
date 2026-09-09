namespace Magnifier.Core;

public interface IWindowEnvironment
{
    ScreenRegion DesktopBounds { get; }
    ScreenRegion GetWindowBounds(nint handle);
    ScreenRegion GetWindowWorkArea(nint handle);
    void PlaceWindow(nint handle, ScreenRegion bounds);
    bool IsRegionVisible(ScreenRegion region);
}
