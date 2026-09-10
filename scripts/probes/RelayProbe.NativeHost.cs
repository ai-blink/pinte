using System.Runtime.InteropServices;
using Magnifier.Core;

internal static partial class RelayProbe
{
    private static readonly NativeWindowProc NativeHostProc = NativeHostMessages;

    private static nint CreateNativeHost(ScreenPoint origin, ScreenPoint end)
    {
        var name = "MagnifierInputTransformProbeHost";
        var windowClass = new NativeWindowClass
        {
            Procedure = Marshal.GetFunctionPointerForDelegate(NativeHostProc),
            Instance = GetModuleHandle(null), Background = 6, ClassName = name
        };
        Check(RegisterClass(ref windowClass) != 0, "검증 부모 class 등록 실패: " + Marshal.GetLastPInvokeError());
        return CreateWindowEx(0x80088, name, "Magnifier native host", 0x82000000,
            origin.X, origin.Y, end.X - origin.X, end.Y - origin.Y, 0, 0, windowClass.Instance, 0);
    }

    private static nint NativeHostMessages(nint hwnd, uint message, nint wParam, nint lParam)
    {
        // child의 Down은 WM_PARENTNOTIFY로 관찰한다. 원본 대상 수신과 구분한다.
        if (message == 0x201 || (message == 0x210 && ((long)wParam & 0xffff) == 0x201)) _lensDowns++;
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private delegate nint NativeWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeWindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassBytes, WindowBytes;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName, ClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref NativeWindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);
}
