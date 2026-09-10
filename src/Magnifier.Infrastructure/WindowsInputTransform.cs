using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

/// <summary>물리 화면 좌표의 원본·렌즈 영역에 Windows 입력 변환을 적용한다.</summary>
/// <remarks>
/// 이 클래스는 포인터 이동이나 버튼 입력을 주입하지 않는다. 호출자는 눌린 입력을
/// 먼저 release한 뒤 변환을 변경하거나 Disable/Dispose를 호출해야 한다.
/// Dispose 실패 뒤에는 IsEnabled로 잔류 상태를 확인하고 Disable/Dispose를 재시도할 수 있다.
/// Windows API는 변환 소유자 식별자를 제공하지 않으므로 성공한 설정의 rect로 소유를 확인한다.
/// </remarks>
public sealed class WindowsInputTransform : IDisposable
{
    // 같은 프로세스의 이 타입 인스턴스끼리 조회와 설정이 엇갈리지 않게 한다.
    private static readonly object Sync = new();
    private NativeRect _source;
    private NativeRect _destination;
    private bool _ownsTransform;
    private bool _initialized;
    private bool _disposeRequested;
    private bool _disposed;

    public WindowsInputTransform()
    {
        lock (Sync)
        {
            if (!MagInitialize()) throw NativeFailure("MagInitialize 초기화 실패");
            _initialized = true;
            try
            {
                var current = ReadTransform("생성 전 상태 확인");
                if (current.Enabled)
                    throw new InvalidOperationException("기존 Windows 입력 변환이 활성 상태입니다. 해당 변환을 변경하지 않았습니다.");
            }
            catch (Exception error)
            {
                try { Uninitialize(); }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("입력 변환 생성과 MagUninitialize 정리가 모두 실패했습니다.",
                        error, cleanupError);
                }
                throw;
            }
        }
    }

    /// <summary>
    /// 이 인스턴스의 변환이 현재 활성인지 OS에서 조회한다. 조회 실패는 예외로 알린다.
    /// Dispose가 정상 완료되면 false이며, 실패 뒤에는 계속 조회할 수 있다.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            lock (Sync)
            {
                if (!_initialized) return false;
                var current = ReadTransform("활성 상태 확인");
                ReconcileOwnership(current);
                return _ownsTransform;
            }
        }
    }

    /// <summary>버튼이 모두 해제된 상태에서 물리 좌표 변환을 설정하고 결과를 확인한다.</summary>
    public void Enable(LensViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        var source = NativeRect.From(viewport.Source, nameof(viewport.Source));
        var destination = NativeRect.From(viewport.Destination, nameof(viewport.Destination));
        lock (Sync)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            var current = ReadTransform("설정 전 상태 확인");
            ReconcileOwnership(current);
            if (current.Enabled && !_ownsTransform)
                throw new InvalidOperationException("다른 Windows 입력 변환이 활성 상태입니다. 기존 변환을 덮어쓰지 않았습니다.");
            if (IsAnyPhysicalButtonDown())
                throw new InvalidOperationException("마우스 버튼이 눌려 있어 입력 변환을 설정하지 않았습니다. 버튼 해제 후 다시 시도하세요.");
            if (current.Enabled && current.Source == source && current.Destination == destination)
                return;

            if (!MagSetInputTransform(true, in source, in destination))
            {
                var error = NativeFailure("MagSetInputTransform 설정 실패");
                // 재설정 실패라도 이전에 소유하던 변환이 남아 있을 수 있다.
                try { ReconcileOwnership(ReadTransform("설정 실패 후 잔류 확인")); }
                catch (Exception readError)
                {
                    throw new AggregateException("입력 변환 설정 실패 뒤 잔류 상태를 확인하지 못했습니다.",
                        error, readError);
                }
                throw error;
            }

            // readback 실패 시에도 성공한 설정을 잊지 않아 해제를 재시도할 수 있다.
            _source = source;
            _destination = destination;
            _ownsTransform = true;
            current = ReadTransform("설정 결과 확인");
            ReconcileOwnership(current);
            if (!_ownsTransform)
                throw ReadBackFailure("MagSetInputTransform 설정 결과가 요청한 활성 상태·rect와 다릅니다. 다른 변환은 변경하지 않았습니다.");
        }
    }

    /// <summary>호출자가 눌린 입력을 release한 뒤 자기 변환만 해제한다. 실패 시 재시도할 수 있다.</summary>
    public void Disable()
    {
        lock (Sync)
        {
            if (!_initialized) return;
            DisableCore();
        }
    }

    public void Dispose()
    {
        lock (Sync)
        {
            if (_disposed) return;
            _disposeRequested = true;
            try { DisableCore(); }
            catch (Exception error)
            {
                // 소유 변환이 남았거나 조회하지 못했다면 초기화를 유지해 재시도한다.
                if (_ownsTransform) throw;
                try { Uninitialize(); }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("입력 변환 해제와 MagUninitialize 정리가 모두 실패했습니다.",
                        error, cleanupError);
                }
                throw;
            }
            Uninitialize();
        }
    }

    private void DisableCore()
    {
        if (!_ownsTransform) return;
        var current = ReadTransform("해제 전 소유 확인");
        ReconcileOwnership(current);
        if (!current.Enabled) return;
        if (!_ownsTransform)
            throw new InvalidOperationException("입력 변환이 다른 rect로 변경되었습니다. 다른 변환을 해제하지 않았습니다.");

        var empty = default(NativeRect);
        if (!MagSetInputTransform(false, in empty, in empty))
        {
            var error = NativeFailure("MagSetInputTransform 해제 실패");
            try { ReconcileOwnership(ReadTransform("해제 실패 후 잔류 확인")); }
            catch (Exception readError)
            {
                throw new AggregateException("입력 변환 해제 실패 뒤 잔류 상태를 확인하지 못했습니다.",
                    error, readError);
            }
            throw error;
        }

        current = ReadTransform("해제 결과 확인");
        ReconcileOwnership(current);
        if (current.Enabled)
            throw ReadBackFailure(_ownsTransform
                ? "MagSetInputTransform 해제 후 자기 변환이 여전히 활성 상태입니다. Disable/Dispose를 재시도하세요."
                : "MagSetInputTransform 해제 후 다른 변환이 활성 상태입니다. 다른 변환은 해제하지 않았습니다.");
    }

    private void ReconcileOwnership(TransformState current)
    {
        if (_ownsTransform && (!current.Enabled || current.Source != _source || current.Destination != _destination))
            _ownsTransform = false;
    }

    private void Uninitialize()
    {
        if (!MagUninitialize()) throw NativeFailure("MagUninitialize 정리 실패. Dispose를 재시도하세요.");
        _initialized = false;
        _disposed = true;
    }

    private static TransformState ReadTransform(string operation)
    {
        if (!MagGetInputTransform(out var enabled, out var source, out var destination))
            throw NativeFailure($"MagGetInputTransform {operation} 실패");
        return new(enabled, source, destination);
    }

    private static bool IsAnyPhysicalButtonDown() =>
        (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0 ||
        (GetAsyncKeyState(4) & 0x8000) != 0 || (GetAsyncKeyState(5) & 0x8000) != 0 ||
        (GetAsyncKeyState(6) & 0x8000) != 0;

    private static Win32Exception NativeFailure(string message)
    {
        var error = Marshal.GetLastPInvokeError();
        return new Win32Exception(error, $"{message} (Win32 오류 {error})");
    }

    // API 호출은 성공했으므로 오래된 GetLastError 값을 readback 불일치의 원인으로 쓰지 않는다.
    private static Win32Exception ReadBackFailure(string message) =>
        new(0, $"{message} (Win32 오류 0: API 호출 성공, 조회 결과 불일치)");

    private readonly record struct TransformState(bool Enabled, NativeRect Source, NativeRect Destination);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeRect(int Left, int Top, int Right, int Bottom)
    {
        public static NativeRect From(ScreenRegion region, string parameter)
        {
            if (region.Width <= 0 || region.Height <= 0)
                throw new ArgumentOutOfRangeException(parameter, "입력 변환 영역의 너비와 높이는 0보다 커야 합니다.");
            var right = (long)region.X + region.Width;
            var bottom = (long)region.Y + region.Height;
            if (right is < int.MinValue or > int.MaxValue || bottom is < int.MinValue or > int.MaxValue)
                throw new ArgumentOutOfRangeException(parameter, "입력 변환 영역의 오른쪽·아래쪽 경계가 signed 32-bit 좌표 범위를 벗어납니다.");
            return new(region.X, region.Y, (int)right, (int)bottom);
        }
    }

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagGetInputTransform(
        [MarshalAs(UnmanagedType.Bool)] out bool enabled, out NativeRect source, out NativeRect destination);

    [DllImport("magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetInputTransform(
        [MarshalAs(UnmanagedType.Bool)] bool enabled, in NativeRect source, in NativeRect destination);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
