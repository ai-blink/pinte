using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed class WindowsPointerInput : IPointerInput
{
    private const uint InputMouse = 0;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventVirtualDesktop = 0x4000;
    private const uint MouseEventAbsolute = 0x8000;
    private const int VirtualScreenLeft = 76;
    private const int VirtualScreenTop = 77;
    private const int VirtualScreenWidth = 78;
    private const int VirtualScreenHeight = 79;

    public void MoveTo(ScreenPoint point)
    {
        var desktop = GetVirtualDesktop();
        SendMouseInput(
            Normalize(point.X, desktop.Left, desktop.Width),
            Normalize(point.Y, desktop.Top, desktop.Height),
            MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesktop);
    }

    public void LeftButtonDown()
    {
        SendMouseInput(0, 0, MouseEventLeftDown);
    }

    public void LeftButtonUp()
    {
        SendMouseInput(0, 0, MouseEventLeftUp);
    }

    private static VirtualDesktop GetVirtualDesktop()
    {
        var desktop = new VirtualDesktop(
            GetSystemMetrics(VirtualScreenLeft),
            GetSystemMetrics(VirtualScreenTop),
            GetSystemMetrics(VirtualScreenWidth),
            GetSystemMetrics(VirtualScreenHeight));

        if (desktop.Width <= 0 || desktop.Height <= 0)
        {
            throw new InvalidOperationException("가상 화면 크기를 읽을 수 없습니다.");
        }

        return desktop;
    }

    private static int Normalize(int coordinate, int origin, int length)
    {
        var denominator = Math.Max(length - 1, 1);
        var normalized = ((long)coordinate - origin) * ushort.MaxValue / denominator;
        return (int)Math.Clamp(normalized, 0, ushort.MaxValue);
    }

    private static void SendMouseInput(int x, int y, uint flags)
    {
        var inputs = new[]
        {
            new Input
            {
                Type = InputMouse,
                Union = new InputUnion
                {
                    Mouse = new MouseInput
                    {
                        X = x,
                        Y = y,
                        Flags = flags
                    }
                }
            }
        };

        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw CreateInputException();
        }
    }

    private static Exception CreateInputException()
    {
        var errorCode = Marshal.GetLastWin32Error();
        if (errorCode == 0)
        {
            return new InvalidOperationException(
                "Windows가 입력 전달을 수락하지 않았습니다. 권한이 높은 대상 창은 입력을 받을 수 없습니다.");
        }

        return new Win32Exception(errorCode, "Windows 입력 전달에 실패했습니다.");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        [In, MarshalAs(UnmanagedType.LPArray)] Input[] inputs,
        int inputSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private readonly record struct VirtualDesktop(int Left, int Top, int Width, int Height);
}
