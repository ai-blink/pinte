using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed class WindowsWindowEnvironment : IWindowEnvironment
{
    private const int ExtendedStyle = -20;
    private const long PassiveOverlayStyles = 0x00080000L | 0x00000020L | 0x08000000L | 0x00000080L;

    public ScreenRegion DesktopBounds => new(GetSystemMetrics(76), GetSystemMetrics(77),
        GetSystemMetrics(78), GetSystemMetrics(79));

    public ScreenRegion GetWindowBounds(nint handle)
    {
        if (!GetWindowRect(handle, out var rect)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public void PlaceWindow(nint handle, ScreenRegion bounds)
    {
        if (!SetWindowPos(handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x14))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void SetPassiveOverlay(nint handle)
    {
        if (handle == 0) throw new ArgumentException("표시 전용 창의 핸들이 필요합니다.", nameof(handle));

        // Layered + Transparent는 윤곽선 위에서도 아래 앱으로 입력을 통과시킨다.
        // 이 창은 relay의 임시 스타일 복원 목록에 넣지 않는다.
        var style = ReadExtendedStyle(handle);
        Marshal.SetLastPInvokeError(0);
        var previous = IntPtr.Size == 8
            ? SetWindowLong64(handle, ExtendedStyle, (nint)((long)style | PassiveOverlayStyles))
            : (nint)SetWindowLong32(handle, ExtendedStyle, (int)((long)style | PassiveOverlayStyles));
        var error = Marshal.GetLastWin32Error();
        if (previous == 0 && error != 0)
            throw new Win32Exception(error, "원본 윤곽선의 클릭 통과 설정에 실패했습니다.");

        if (((long)ReadExtendedStyle(handle) & PassiveOverlayStyles) != PassiveOverlayStyles)
            throw new InvalidOperationException("원본 윤곽선의 클릭 통과·비활성 설정을 확인하지 못했습니다.");

        // HWND_TOPMOST; 위치·크기·활성 창은 바꾸지 않고 스타일 변경을 적용한다.
        if (!SetWindowPos(handle, new nint(-1), 0, 0, 0, 0, 0x33))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "원본 윤곽선을 최상위로 표시하지 못했습니다.");
    }

    private static nint ReadExtendedStyle(nint handle)
    {
        Marshal.SetLastPInvokeError(0);
        var style = IntPtr.Size == 8 ? GetWindowLong64(handle, ExtendedStyle) : GetWindowLong32(handle, ExtendedStyle);
        var error = Marshal.GetLastWin32Error();
        if (style == 0 && error != 0)
            throw new Win32Exception(error, "원본 윤곽선 창의 스타일을 확인하지 못했습니다.");
        return style;
    }

    public ScreenRegion GetWindowWorkArea(nint handle)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
    }

    public bool IsRegionVisible(ScreenRegion region)
    {
        // All corners must belong to a monitor, including desktops with gaps.
        return HasMonitor(region.X, region.Y) && HasMonitor(region.X + region.Width - 1, region.Y)
            && HasMonitor(region.X, region.Y + region.Height - 1)
            && HasMonitor(region.X + region.Width - 1, region.Y + region.Height - 1);
    }

    private static bool HasMonitor(int x, int y) => MonitorFromPoint(new Point(x, y), 0) != 0;
    [StructLayout(LayoutKind.Sequential)] private readonly record struct Point(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint handle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLong64(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong32(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLong64(nint hwnd, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong32(nint hwnd, int index, int value);
}
