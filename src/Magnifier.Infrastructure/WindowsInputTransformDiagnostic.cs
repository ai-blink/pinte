using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Magnifier.Core;
using Microsoft.Win32.SafeHandles;

namespace Magnifier.Infrastructure;

/// <summary>
/// 명시적 진단 전용. 같은 source/destination으로 권한 경계만 검사한다.
/// 입력을 주입하지 않으며 다른 돋보기가 사용 중인 변환은 변경하지 않는다.
/// </summary>
public static class WindowsInputTransformDiagnostic
{
    public static InputTransformDiagnosticResult Run()
    {
        var result = new InputTransformDiagnosticResult();
        var initialized = false;
        var applied = false;
        NativeRect identity = default;
        try
        {
            using (var process = Process.GetCurrentProcess())
            {
                if (!OpenProcessToken(process.Handle, 0x0008, out var token))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "프로세스 토큰 조회 실패");
                using (token)
                {
                    result.TokenUIAccess = ReadTokenFlag(token, 26);
                    result.TokenElevated = ReadTokenFlag(token, 20);
                }
            }

            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                result.Result = "BLOCKED_WOW64";
                return result;
            }
            initialized = Record(result, "MagInitialize", MagInitialize());
            if (!initialized) return result;
            if (!Record(result, "MagGetInputTransform(before)",
                    MagGetInputTransform(out var enabled, out var source, out var destination))) return result;
            result.InitialTransformEnabled = enabled;
            if (enabled)
            {
                result.Result = "BLOCKED_EXISTING_TRANSFORM";
                return result;
            }
            if (new[] { 1, 2, 4, 5, 6 }.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
            {
                result.Result = "NEEDS_USER_UI_CHECK_BUTTON_HELD";
                return result;
            }

            var screen = new ScreenRegion(GetSystemMetrics(76), GetSystemMetrics(77),
                GetSystemMetrics(78), GetSystemMetrics(79));
            var viewport = new LensViewport(screen, screen);
            result.Viewport = viewport;
            source = NativeRect.From(viewport.Source);
            destination = NativeRect.From(viewport.Destination);
            identity = source;
            applied = Record(result, "MagSetInputTransform(identity)",
                MagSetInputTransform(true, in source, in destination));
            if (!applied)
            {
                result.Result = result.Calls[^1].Win32Error == 5 && result.TokenUIAccess == false
                    ? "BLOCKED_UIACCESS_ACCESS_DENIED" : "BLOCKED_SET_INPUT_TRANSFORM";
                return result;
            }
            if (Record(result, "MagGetInputTransform(applied)",
                    MagGetInputTransform(out enabled, out source, out destination)))
            {
                result.IdentityReadBackMatches = enabled && source == identity && destination == identity;
                result.Result = result.IdentityReadBackMatches == true
                    ? "API_ACCEPTED_NOT_UI_VERIFIED" : "BLOCKED_TRANSFORM_READBACK_MISMATCH";
            }
        }
        catch (Exception ex)
        {
            result.Result = "DIAGNOSTIC_ERROR";
            result.Error = ex.Message;
        }
        finally
        {
            if (initialized)
            {
                // 성공한 설정만 소유한다. 그 사이 다른 변환이 설정됐다면 지우지 않는다.
                var read = Record(result, "MagGetInputTransform(cleanup)",
                    MagGetInputTransform(out var enabled, out var source, out var destination));
                if (applied && read && enabled && source == identity && destination == identity)
                {
                    var empty = default(NativeRect);
                    Record(result, "MagSetInputTransform(disable)",
                        MagSetInputTransform(false, in empty, in empty));
                    read = Record(result, "MagGetInputTransform(final)",
                        MagGetInputTransform(out enabled, out source, out destination));
                }
                result.FinalTransformEnabled = read ? enabled : null;
                result.Restored = read && !enabled && result.InitialTransformEnabled == false;
                if (applied && !result.Restored) result.Result = "BLOCKED_TRANSFORM_CLEANUP";
                if (!Record(result, "MagUninitialize", MagUninitialize()))
                    result.Result = "BLOCKED_MAG_UNINITIALIZE";
            }
        }
        return result;
    }

    private static bool ReadTokenFlag(SafeAccessTokenHandle token, int informationClass)
    {
        if (!GetTokenInformation(token, informationClass, out var value, sizeof(int), out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "토큰 속성 조회 실패");
        return value != 0;
    }

    private static bool Record(InputTransformDiagnosticResult result, string api, bool success)
    {
        var error = success ? 0 : Marshal.GetLastPInvokeError();
        result.Calls.Add(new(api, success, error));
        return success;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeRect(int Left, int Top, int Right, int Bottom)
    {
        public static NativeRect From(ScreenRegion region) => new(region.X, region.Y,
            checked(region.X + region.Width), checked(region.Y + region.Height));
    }

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool MagInitialize();
    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool MagUninitialize();
    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool MagGetInputTransform(
        [MarshalAs(UnmanagedType.Bool)] out bool enabled, out NativeRect source, out NativeRect destination);
    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool MagSetInputTransform(
        [MarshalAs(UnmanagedType.Bool)] bool enabled, in NativeRect source, in NativeRect destination);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(
        nint process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(
        SafeAccessTokenHandle token, int informationClass, out int value, int size, out int length);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}

public sealed class InputTransformDiagnosticResult
{
    public string Result { get; internal set; } = "BLOCKED_NATIVE_CALL";
    public string? Executable { get; } = Environment.ProcessPath;
    public string ProcessArchitecture { get; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string OSVersion { get; } = Environment.OSVersion.VersionString;
    public Guid EngineBuild { get; } = typeof(WindowsInputTransformDiagnostic).Assembly.ManifestModule.ModuleVersionId;
    public bool? TokenUIAccess { get; internal set; }
    public bool? TokenElevated { get; internal set; }
    public bool? InitialTransformEnabled { get; internal set; }
    public bool? FinalTransformEnabled { get; internal set; }
    public bool? IdentityReadBackMatches { get; internal set; }
    public bool Restored { get; internal set; }
    public LensViewport? Viewport { get; internal set; }
    public List<InputTransformNativeCall> Calls { get; } = [];
    public string? Error { get; internal set; }
    public bool TargetResponseVerified => false;
    public bool SingleCursorVerified => false;
}

public sealed record InputTransformNativeCall(string Api, bool Success, int Win32Error);
