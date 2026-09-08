using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed class WindowsScreenCapture : IScreenCapture
{
    private const uint Bgr32 = 0;
    private const uint SourceCopy = 0x00CC0020;
    private const uint CaptureLayeredWindows = 0x40000000;

    public CapturedFrame Capture(ScreenRegion region)
    {
        var screenDeviceContext = GetDC(IntPtr.Zero);
        if (screenDeviceContext == IntPtr.Zero)
        {
            throw CreateCaptureException("화면 장치 컨텍스트를 열 수 없습니다.");
        }

        IntPtr memoryDeviceContext = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previousBitmap = IntPtr.Zero;

        try
        {
            memoryDeviceContext = CreateCompatibleDC(screenDeviceContext);
            if (memoryDeviceContext == IntPtr.Zero)
            {
                throw CreateCaptureException("캡처용 장치 컨텍스트를 만들 수 없습니다.");
            }

            var bitmapInfo = CreateBitmapInfo(region);
            bitmap = CreateDIBSection(screenDeviceContext, ref bitmapInfo, Bgr32, out var pixels, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || pixels == IntPtr.Zero)
            {
                throw CreateCaptureException("캡처용 비트맵을 만들 수 없습니다.");
            }

            previousBitmap = SelectObject(memoryDeviceContext, bitmap);
            if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1))
            {
                throw CreateCaptureException("캡처용 비트맵을 선택할 수 없습니다.");
            }

            if (!BitBlt(
                    memoryDeviceContext,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    screenDeviceContext,
                    region.X,
                    region.Y,
                    SourceCopy | CaptureLayeredWindows))
            {
                throw CreateCaptureException("선택한 화면 영역을 복사할 수 없습니다.");
            }

            var bytes = new byte[checked(region.Width * region.Height * 4)];
            Marshal.Copy(pixels, bytes, 0, bytes.Length);
            SetOpaqueAlpha(bytes);
            return new CapturedFrame(region, bytes);
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1))
            {
                SelectObject(memoryDeviceContext, previousBitmap);
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memoryDeviceContext != IntPtr.Zero)
            {
                DeleteDC(memoryDeviceContext);
            }

            ReleaseDC(IntPtr.Zero, screenDeviceContext);
        }
    }

    private static BitmapInfo CreateBitmapInfo(ScreenRegion region)
    {
        return new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = region.Width,
                Height = -region.Height,
                Planes = 1,
                BitCount = 32,
                Compression = Bgr32
            }
        };
    }

    private static void SetOpaqueAlpha(byte[] bytes)
    {
        for (var index = 3; index < bytes.Length; index += 4)
        {
            bytes[index] = byte.MaxValue;
        }
    }

    private static Win32Exception CreateCaptureException(string message)
    {
        return new Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        [In] ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr pixels,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        IntPtr destinationDeviceContext,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr sourceDeviceContext,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }
}
