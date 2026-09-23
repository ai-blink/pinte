param(
    [long[]]$WindowHandles = @(66020, 265808, 3347148),
    [int]$X = 3120,
    [int]$Y = 1850,
    [ValidateRange(0, 30000)][int]$SampleMilliseconds = 0,
    [long]$ObservedWindow = 3347148
)

# Read-only snapshot. Never moves the cursor, sends input or changes another window.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
public static class RelayWindowRead {
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
 public delegate bool EnumProc(IntPtr h, IntPtr p);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder b, int n);
 [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetStyle(IntPtr h, int i);
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
 [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
 public static object[] Sample(IntPtr observedWindow, int milliseconds) {
   var changes = new List<object>();
   var timer = Stopwatch.StartNew();
   bool lastDown = false;
   long lastStyle = -1;
   while(timer.ElapsedMilliseconds < milliseconds) {
     Point cursor;
     bool available = GetCursorPos(out cursor);
     bool down = (GetAsyncKeyState(1) & 0x8000) != 0;
     long style = GetStyle(observedWindow, -20).ToInt64();
     if (style != lastStyle || down != lastDown) {
       var hit = WindowFromPoint(cursor);
       changes.Add(new { At=DateTimeOffset.Now, X=cursor.X, Y=cursor.Y, CursorAvailable=available,
         LeftDown=down, ExStyle=style, HitRoot=GetAncestor(hit,2).ToInt64(),
         Foreground=GetForegroundWindow().ToInt64() });
       lastDown=down; lastStyle=style;
     }
     Thread.Sleep(2);
   }
   return changes.ToArray();
 }
}
'@

function Read-RelayWindow([IntPtr]$WindowHandle) {
    $procNumber = 0u
    $threadNumber = [RelayWindowRead]::GetWindowThreadProcessId($WindowHandle, [ref]$procNumber)
    $bounds = [RelayWindowRead+Rect]::new()
    [void][RelayWindowRead]::GetWindowRect($WindowHandle, [ref]$bounds)
    $className = [Text.StringBuilder]::new(256)
    [void][RelayWindowRead]::GetClassName($WindowHandle, $className, 256)
    $process = Get-Process -Id $procNumber -ErrorAction SilentlyContinue
    [pscustomobject]@{
        Hwnd = $WindowHandle.ToInt64()
        Exists = [RelayWindowRead]::IsWindow($WindowHandle)
        Process = $process.ProcessName
        Pid = $procNumber
        Thread = $threadNumber
        Class = $className.ToString()
        ExStyle = ('0x{0:X}' -f [RelayWindowRead]::GetStyle($WindowHandle, -20).ToInt64())
        Rect = "$($bounds.L),$($bounds.T),$($bounds.R),$($bounds.B)"
    }
}

$oldDpi = [RelayWindowRead]::SetThreadDpiAwarenessContext([IntPtr](-4))
try {
    if ($SampleMilliseconds -gt 0) {
        ConvertTo-Json -InputObject @([RelayWindowRead]::Sample([IntPtr]$ObservedWindow, $SampleMilliseconds)) -Depth 4
        return
    }
    $point = [RelayWindowRead+Point]::new()
    $point.X = $X
    $point.Y = $Y
    $cursorPoint = [RelayWindowRead+Point]::new()
    $cursorAvailable = [RelayWindowRead]::GetCursorPos([ref]$cursorPoint)
    $candidates = [Collections.Generic.List[IntPtr]]::new()
    $callback = [RelayWindowRead+EnumProc]{ param($handle, $unused)
        $rect = [RelayWindowRead+Rect]::new()
        if ([RelayWindowRead]::IsWindowVisible($handle) -and
            [RelayWindowRead]::GetWindowRect($handle, [ref]$rect) -and
            $rect.L -le $X -and $rect.R -gt $X -and $rect.T -le $Y -and $rect.B -gt $Y) {
            $candidates.Add($handle)
        }
        return $true
    }
    [void][RelayWindowRead]::EnumWindows($callback, [IntPtr]::Zero)
    [pscustomobject]@{
        At = [DateTimeOffset]::Now
        Point = $point
        CursorAvailable = $cursorAvailable
        Cursor = $cursorPoint
        Foreground = Read-RelayWindow ([RelayWindowRead]::GetForegroundWindow())
        WindowAtPoint = Read-RelayWindow ([RelayWindowRead]::WindowFromPoint($point))
        LoggedHandlesNow = @($WindowHandles | ForEach-Object { Read-RelayWindow ([IntPtr]$_) })
        CoveringWindows = @($candidates | ForEach-Object { Read-RelayWindow $_ })
    } | ConvertTo-Json -Depth 5
}
finally {
    [void][RelayWindowRead]::SetThreadDpiAwarenessContext($oldDpi)
}
