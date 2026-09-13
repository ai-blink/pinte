namespace Magnifier.Core;

public interface IWindowEnvironment
{
    ScreenRegion DesktopBounds { get; }
    ScreenRegion GetWindowBounds(nint handle);
    ScreenRegion GetWindowWorkArea(nint handle);
    void PlaceWindow(nint handle, ScreenRegion bounds);
    /// <summary>표시 전용 창을 영구 클릭 통과·비활성·최상위 상태로 만든다. 실패하면 예외를 낸다.</summary>
    void SetPassiveOverlay(nint handle);
    bool IsRegionVisible(ScreenRegion region);
}
