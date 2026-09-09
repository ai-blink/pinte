using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

// A dedicated message thread owns hook, logical cursor and the entire input session.
// WPF never captures the forwarded button: the actual application can own capture.
public sealed partial class WindowsLivePointerRelay : ILivePointerRelay
{
    private readonly WindowsPointerInput _output = new();
    private readonly LensInputState _state;
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HookProc _mouseProc;
    private readonly Dictionary<nint, nint> _savedStyles = [];
    private readonly Thread _thread;
    private LensViewport? _viewport;
    private nint[] _windows = [];
    private uint _threadId;
    private long _frameTimestamp;
    private bool _relaying, _leftHeld, _draining, _disposed, _intercepting, _inputPostFailed;
    private int _hookButtons;
    private int _otherButtonsHeld;
    private bool OtherHeld => _otherButtonsHeld != 0;
    private PreviewPoint _logical;
    private nint _observedCapture;
    private string _message = "보기 · 실제 입력 꺼짐";

    public WindowsLivePointerRelay()
    {
        _state = new LensInputState(_output);
        _mouseProc = MouseHook;
        _thread = new Thread(Run) { IsBackground = true, Name = "Magnifier pointer relay" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public event Action<RelayStatus>? StatusChanged;

    public Task ConfigureAsync(LensViewport viewport, params nint[] overlayWindows) => Dispatch(() =>
    {
        if (_viewport == viewport && _windows.SequenceEqual(overlayWindows)) return;
        StopInternal("배치 변경 · 보기로 전환");
        EnsureReleased();
        _viewport = viewport;
        _windows = overlayWindows.Where(x => x != 0).Distinct().ToArray();
    });

    public async Task<bool> StartAsync()
    {
        var armed = false;
        await Dispatch(() =>
        {
            _leftHeld = (GetAsyncKeyState(1) & 0x8000) != 0;
            _otherButtonsHeld = ReadOtherButtons();
            _hookButtons = (_leftHeld ? 1 : 0) | (_otherButtonsHeld << 1);
            if (_viewport is null || !FrameIsFresh())
            { Publish("최신 화면을 기다리는 중 · 조작을 시작하지 않았습니다"); return; }
            armed = _state.Arm(_leftHeld || OtherHeld || _draining);
            Publish(armed ? "조작 켜짐 · 렌즈 안으로 이동하세요" : "마우스 버튼을 놓은 뒤 조작 시작을 누르세요");
        });
        return armed;
    }

    public Task StopAsync(string reason) => Dispatch(() => { StopInternal(reason); EnsureReleased(); });
    private void EnsureReleased()
    {
        if (_state.IsPressed) throw new InvalidOperationException("Windows가 버튼 해제를 수락하지 않았습니다. 중지를 다시 눌러 해제를 재시도하세요.");
    }
    public void RefreshFrame() => Interlocked.Exchange(ref _frameTimestamp, Environment.TickCount64);
    private bool FrameIsFresh() => Environment.TickCount64 - Interlocked.Read(ref _frameTimestamp) < 750;

    private async Task Dispatch(Action action)
    {
        await _ready.Task.ConfigureAwait(false);
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsLivePointerRelay));
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() => { try { action(); done.SetResult(); } catch (Exception ex) { done.SetException(ex); } });
        if (!PostThreadMessage(_threadId, CommandMessage, 0, 0))
            throw new InvalidOperationException("입력 스레드에 요청을 전달하지 못했습니다.");
        await done.Task.ConfigureAwait(false);
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, 0, 0, 0, 0);
        var hook = SetWindowsHookEx(14, _mouseProc, GetModuleHandle(null), 0);
        if (hook == 0) { _ready.SetException(new Win32Exception(Marshal.GetLastWin32Error(), "마우스 중계 설치 실패")); return; }
        var timer = SetTimer(0, 0, 50, 0);
        if (timer == 0)
        {
            UnhookWindowsHookEx(hook);
            _ready.SetException(new Win32Exception(Marshal.GetLastWin32Error(), "입력 해제 타이머 설치 실패"));
            return;
        }
        _ready.SetResult();
        try
        {
            while (GetMessage(out var msg, 0, 0, 0) > 0)
            {
                if (msg.Id == CommandMessage) while (_commands.TryDequeue(out var action)) action();
                if (msg.Id == 0x113) CheckSession();
            }
        }
        finally
        {
            try { StopInternal("입력 중계 종료"); } catch { }
            KillTimer(0, timer);
            UnhookWindowsHookEx(hook);
        }
    }

    private nint MouseHook(int code, nint message, nint data)
    {
        if (code < 0) return CallNextHookEx(0, code, message, data);
        var mouse = Marshal.PtrToStructure<MouseHookData>(data);
        if (mouse.ExtraInfo == WindowsPointerInput.InjectionTag)
            return CallNextHookEx(0, code, message, data);
        var kind = (uint)message;
        var previousButtons = _hookButtons;
        var bit = kind switch
        {
            0x201 or 0x202 => 1,
            0x204 or 0x205 => 2,
            0x207 or 0x208 => 4,
            0x20B or 0x20C => (mouse.MouseData >> 16) == 1 ? 8 : 16,
            _ => 0
        };
        if (kind is 0x201 or 0x204 or 0x207 or 0x20B) _hookButtons |= bit;
        if (kind is 0x202 or 0x205 or 0x208 or 0x20C) _hookButtons &= ~bit;
        var intercepted = _intercepting || _draining || (_state.IsEnabled && previousButtons == 0
            && _viewport is { } view && view.Contains(new(mouse.Point.X, mouse.Point.Y)));
        if (intercepted && !_draining) _intercepting = true;
        var passMove = _draining && kind == 0x200;
        var hasPrevious = GetCursorPos(out var previous);
        // Do not call SendInput/SetWindowLong from this callback. Nested input routing
        // can deliver the original button before the hook's suppression result returns.
        _commands.Enqueue(() => ProcessMouse(kind, mouse, intercepted, previous, hasPrevious));
        if (!PostThreadMessage(_threadId, CommandMessage, 0, 0)) _inputPostFailed = true;
        return intercepted && !passMove ? 1 : CallNextHookEx(0, code, message, data);
    }

    private void ProcessMouse(uint kind, MouseHookData mouse, bool intercepted, NativePoint previous, bool hasPrevious)
    {
        if (kind == 0x201) _leftHeld = true;
        if (kind == 0x202) _leftHeld = false;
        var otherBit = kind switch
        {
            0x204 or 0x205 => 1,
            0x207 or 0x208 => 2,
            0x20B or 0x20C => (mouse.MouseData >> 16) == 1 ? 4 : 8,
            _ => 0
        };
        if (kind is 0x204 or 0x207 or 0x20B) _otherButtonsHeld |= otherBit;
        if (kind is 0x205 or 0x208 or 0x20C) _otherButtonsHeld &= ~otherBit;
        _state.ObservePhysicalButton(_leftHeld || OtherHeld);
        if (!intercepted) return;
        try
        {
            if (_draining)
            {
                // The held button belongs to the old drag, never to a return button.
                if (!_leftHeld && !OtherHeld) { _draining = false; Publish("보기 · 버튼 해제됨"); }
                return;
            }
            if (!_state.IsEnabled || _viewport is not { } view)
                return;
            if (!FrameIsFresh()) { StopInternal("화면 갱신 지연 · 조작 중지"); return; }
            if (!_relaying)
            {
                if (!view.Contains(new ScreenPoint(mouse.Point.X, mouse.Point.Y)))
                    return;
                // Do not steal a drag that started in another window or a window handle.
                if ((_leftHeld || OtherHeld) && kind != 0x201)
                    return;
                _logical = new(mouse.Point.X, mouse.Point.Y);
                SetPassthrough(true);
                _relaying = true;
                _output.MoveTo(view.MapToSource(_logical));
            }
            else if (kind == 0x200)
            {
                if ((mouse.Flags & 1) != 0)
                    _logical = new(mouse.Point.X, mouse.Point.Y); // Absolute assistive pointer updates.
                else
                {
                    if (!hasPrevious) throw new InvalidOperationException("포인터 위치 읽기 실패");
                    _logical = new(_logical.X + mouse.Point.X - previous.X, _logical.Y + mouse.Point.Y - previous.Y);
                }
            }
            var logicalPixel = new ScreenPoint((int)Math.Floor(_logical.X), (int)Math.Floor(_logical.Y));
            if (!view.Contains(logicalPixel))
            { StopInternal("경계에서 버튼 해제 · 보기로 전환"); return; }
            var target = view.MapToSource(_logical);
            switch (kind)
            {
                case 0x201: _state.Begin(target); _observedCapture = 0; break;
                case 0x200:
                    if (_state.IsPressed) _state.Move(target); else _output.MoveTo(target);
                    break;
                case 0x202: _state.Complete(target); _observedCapture = 0; break;
                default:
                    StopInternal("이 조작은 아직 직접 전달하지 않습니다 · 보기로 전환");
                    return;
            }
            Publish(_state.IsPressed ? "드래그 중 · 가장자리로 이동하면 중지" : "조작 켜짐 · 실제 대상 반응을 확인하세요");
        }
        catch (Exception ex)
        {
            try { StopInternal($"입력 실패: {ex.Message}"); } catch { }
        }
    }

    private void StopInternal(string reason)
    {
        var restore = _relaying;
        _draining |= (restore || _intercepting) && (_leftHeld || OtherHeld);
        _intercepting = false;
        try { _state.Stop(); }
        catch (Exception ex) { reason += $" · 해제 재시도 필요: {ex.Message}"; }
        finally
        {
            _relaying = false;
            _observedCapture = 0;
            SetPassthrough(false);
            // Up is sent before this move, preventing a long stroke towards the toolbar.
            if (restore && !_state.IsPressed)
                _output.MoveTo(new ScreenPoint((int)Math.Round(_logical.X), (int)Math.Round(_logical.Y)));
            Publish(reason);
        }
    }

    private void CheckSession()
    {
        try
        {
            if (_inputPostFailed) { _inputPostFailed = false; StopInternal("입력 큐 전달 실패 · 조작 중지"); return; }
            if (_state.IsPressed && !_state.IsEnabled) { _state.Stop(); Publish(_message); }
            if (!_state.IsEnabled) return;
            if (!FrameIsFresh()) { StopInternal("화면 갱신 지연 · 조작 중지"); return; }
            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) { StopInternal("Esc · 보기로 전환"); return; }
            var desktop = OpenInputDesktop(0, false, 1);
            if (desktop == 0) { StopInternal("입력 화면 변경 · 조작 중지"); return; }
            CloseDesktop(desktop);
            if (_state.IsPressed)
            {
                var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
                if (GetGUIThreadInfo(0, ref info))
                {
                    if (_observedCapture != 0 && info.Capture != _observedCapture)
                    { StopInternal("대상 포인터 capture 손실 · 조작 중지"); return; }
                    if (info.Capture != 0) _observedCapture = info.Capture;
                }
            }
        }
        catch (Exception ex) { try { StopInternal($"입력 상태 확인 실패: {ex.Message}"); } catch { } }
    }

    private static int ReadOtherButtons() => ((GetAsyncKeyState(2) & 0x8000) != 0 ? 1 : 0)
        | ((GetAsyncKeyState(4) & 0x8000) != 0 ? 2 : 0)
        | ((GetAsyncKeyState(5) & 0x8000) != 0 ? 4 : 0)
        | ((GetAsyncKeyState(6) & 0x8000) != 0 ? 8 : 0);

    private void SetPassthrough(bool enabled)
    {
        if (enabled)
        {
            foreach (var hwnd in _windows)
            {
                var style = GetWindowLongPtr(hwnd, ExtendedStyle);
                if (((long)style & LayeredStyle) == 0) throw new InvalidOperationException("입력 표면이 layered 창이 아닙니다.");
                _savedStyles.TryAdd(hwnd, style);
                Marshal.SetLastPInvokeError(0);
                if (SetWindowLongPtr(hwnd, ExtendedStyle, (nint)((long)style | TransparentStyle)) == 0 && Marshal.GetLastWin32Error() != 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "창 입력 통과 설정 실패");
            }
        }
        else
        {
            foreach (var (hwnd, style) in _savedStyles) SetWindowLongPtr(hwnd, ExtendedStyle, style);
            _savedStyles.Clear();
        }
    }

    private void Publish(string message)
    {
        _message = message;
        StatusChanged?.Invoke(new(_state.IsEnabled, _relaying, _state.IsPressed,
            _state.IsWaitingForRelease || _draining, _logical, message));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (_ready.Task.IsFaulted) { _disposed = true; return; }
        await StopAsync("종료 · 입력 해제");
        _disposed = true;
        PostThreadMessage(_threadId, 0x12, 0, 0);
    }
}
