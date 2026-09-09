using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed class WindowsWindowEnvironment : IWindowEnvironment
{
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
}
