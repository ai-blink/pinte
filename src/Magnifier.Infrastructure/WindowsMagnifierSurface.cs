using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

/// <summary>기존 부모 창 안에 원본 화면을 확대하는 네이티브 Magnifier 자식 창을 만든다.</summary>
/// <remarks>
/// 호출자는 같은 UI 스레드에서 Magnification 런타임을 먼저 초기화하고, 이 창을 Dispose한 뒤
/// 런타임을 해제한다. 부모 창은 WS_EX_LAYERED와 불투명 layered 속성을 갖춰야 한다.
/// 이 타입은 부모 스타일, 전역 배율, 입력 변환, 시스템 커서 표시 상태를 변경하지 않는다.
/// </remarks>
public sealed class WindowsMagnifierSurface : IDisposable
{
    private const uint ChildStyle = 0x40000000;
    private const uint VisibleStyle = 0x10000000;
    private const uint ShowMagnifiedCursorStyle = 0x0001;
    private const uint ExcludeFilterMode = 0;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;

    /// <param name="parentWindow">호출 UI 스레드가 소유하는 기존 부모 HWND.</param>
    /// <param name="viewport">원본과 렌즈 콘텐츠의 데스크톱 물리 픽셀 영역.</param>
    /// <param name="parentOrigin">부모 client 영역 좌측 상단의 데스크톱 물리 픽셀 좌표.</param>
    public WindowsMagnifierSurface(nint parentWindow, LensViewport viewport, ScreenPoint parentOrigin)
    {
        if (parentWindow == 0) throw new ArgumentException("부모 창 HWND가 필요합니다.", nameof(parentWindow));
        ArgumentNullException.ThrowIfNull(viewport);
        var source = NativeRect.From(viewport.Source, nameof(viewport.Source));
        var destination = NativeRect.From(viewport.Destination, nameof(viewport.Destination));
        var clientX = ToCoordinate((long)destination.Left - parentOrigin.X, nameof(parentOrigin));
        var clientY = ToCoordinate((long)destination.Top - parentOrigin.Y, nameof(parentOrigin));
        ToCoordinate((long)destination.Right - parentOrigin.X, nameof(parentOrigin));
        ToCoordinate((long)destination.Bottom - parentOrigin.Y, nameof(parentOrigin));

        var instance = GetModuleHandle(null);
        if (instance == 0) throw NativeFailure("GetModuleHandle 모듈 조회 실패");
        Handle = CreateWindowEx(0, "Magnifier", "Magnifier surface",
            ChildStyle | VisibleStyle | ShowMagnifiedCursorStyle,
            clientX, clientY, viewport.Destination.Width, viewport.Destination.Height,
            parentWindow, 0, instance, 0);
        if (Handle == 0) throw NativeFailure("CreateWindowEx Magnifier 자식 창 생성 실패");

        try
        {
            // MAGTRANSFORM은 row-major float[3][3]이며 확대 배율은 대각 원소다.
            var transform = new NativeTransform(
                (float)viewport.Destination.Width / viewport.Source.Width, 0, 0,
                0, (float)viewport.Destination.Height / viewport.Source.Height, 0,
                0, 0, 1);
            if (!MagSetWindowTransform(Handle, ref transform))
                throw NativeFailure("MagSetWindowTransform 확대 배율 설정 실패");
            // MagSetWindowSource의 RECT는 포인터가 아닌 값으로 전달한다.
            if (!MagSetWindowSource(Handle, source))
                throw NativeFailure("MagSetWindowSource 원본 영역 설정 실패");
            nint[] excludedWindows = [parentWindow, Handle];
            if (!MagSetWindowFilterList(Handle, ExcludeFilterMode, excludedWindows.Length, excludedWindows))
                throw NativeFailure("MagSetWindowFilterList 부모·자기 창 제외 설정 실패");
            Refresh();
        }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanupError)
            {
                throw new AggregateException($"Magnifier 설정 실패 후 자식 HWND {Handle} 정리도 실패했습니다.",
                    error, cleanupError);
            }
            throw;
        }
    }

    /// <summary>생성한 자식 HWND. 정상 Dispose 뒤에는 0이다.</summary>
    public nint Handle { get; private set; }

    /// <summary>자식 창에 다시 그리기를 요청한다. 호출 UI 스레드의 메시지 루프가 갱신을 처리한다.</summary>
    public void Refresh()
    {
        EnsureOwnerThread();
        ObjectDisposedException.ThrowIf(Handle == 0, this);
        if (!InvalidateRect(Handle, 0, true))
            throw NativeFailure("InvalidateRect Magnifier 화면 갱신 요청 실패");
    }

    /// <summary>부모 창과 Magnification 런타임보다 먼저 같은 UI 스레드에서 호출한다.</summary>
    public void Dispose()
    {
        if (Handle == 0) return;
        EnsureOwnerThread();
        if (!DestroyWindow(Handle))
            throw NativeFailure("DestroyWindow Magnifier 자식 창 해제 실패");
        Handle = 0;
    }

    private void EnsureOwnerThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Magnifier 자식 창은 생성한 UI 스레드에서 갱신·해제해야 합니다.");
    }

    private static int ToCoordinate(long value, string parameter)
    {
        if (value is < int.MinValue or > int.MaxValue)
            throw new ArgumentOutOfRangeException(parameter, "Magnifier 좌표가 signed 32-bit 범위를 벗어납니다.");
        return (int)value;
    }

    private static Win32Exception NativeFailure(string message)
    {
        var error = Marshal.GetLastPInvokeError();
        return new Win32Exception(error, $"{message} (Win32 오류 {error})");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeRect(int Left, int Top, int Right, int Bottom)
    {
        public static NativeRect From(ScreenRegion region, string parameter)
        {
            if (region.Width <= 0 || region.Height <= 0)
                throw new ArgumentOutOfRangeException(parameter, "Magnifier 영역의 너비와 높이는 0보다 커야 합니다.");
            return new(region.X, region.Y,
                ToCoordinate((long)region.X + region.Width, parameter),
                ToCoordinate((long)region.Y + region.Height, parameter));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeTransform(
        float M00, float M01, float M02,
        float M10, float M11, float M12,
        float M20, float M21, float M22);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetWindowTransform(nint window, ref NativeTransform transform);

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetWindowSource(nint window, NativeRect source);

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetWindowFilterList(nint window, uint filterMode, int count, [In] nint[] windows);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InvalidateRect(nint window, nint rectangle, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
}
