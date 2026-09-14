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
    private readonly RelayCommandPump _commands = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HookProc _mouseProc;
    private readonly Dictionary<nint, nint> _savedStyles = [];
    private readonly Thread _thread;
    private LensViewport? _viewport;
    private nint[] _windows = [];
    private uint _threadId;
    private readonly FrameFreshnessGate _frames = new();
    private bool _relaying, _leftHeld, _draining, _disposed, _intercepting, _inputPostFailed, _suspended;
    private int _hookButtons;
    private int _otherButtonsHeld;
    private bool OtherHeld => _otherButtonsHeld != 0;
    private PreviewPoint _logical;
    private readonly PointerCaptureMonitor _captureMonitor = new();
    private readonly HookLivenessMonitor _hookLiveness = new();
    private nint _hook;
    private int _hookReinstallCount;
    private bool _hookReinstallDisabled;
    private string _message = "보기 · 실제 입력 꺼짐";

    public WindowsLivePointerRelay()
    {
        _state = new LensInputState(_output);
        _mouseProc = MouseHook;
        // A starved hook thread trips LowLevelHooksTimeout, after which Windows drops the hook.
        _thread = new Thread(Run) { IsBackground = true, Name = "Magnifier pointer relay", Priority = ThreadPriority.Highest };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public event Action<RelayStatus>? StatusChanged;

    public Task ConfigureAsync(LensViewport viewport, params nint[] overlayWindows) => Dispatch(() =>
    {
        if (_viewport == viewport && _windows.SequenceEqual(overlayWindows)) return;
        StopInternal("배치 변경 · 조작 자동 재개 대기", resume: true);
        EnsureReleased();
        _frames.RequireNextFrame();
        _viewport = viewport;
        _windows = overlayWindows.Where(x => x != 0).Distinct().ToArray();
    });

    public async Task<bool> StartAsync()
    {
        var armed = false;
        await Dispatch(() =>
        {
            _state.RequestStart();
            TryResumeInput();
            armed = _state.IsEnabled;
            Publish(armed ? "조작 켜짐 · 렌즈 안으로 이동하세요" : "버튼 해제와 최신 화면을 기다린 뒤 자동 재개합니다");
        });
        return armed;
    }

    public Task StopAsync(string reason) => Dispatch(() => { StopInternal(reason); EnsureReleased(); });
    public Task PauseAsync(string reason) => Dispatch(() =>
    {
        StopInternal(reason, resume: true);
        _frames.RequireNextFrame();
        EnsureReleased();
    });
    public Task SetSuspendedAsync(bool suspended, string reason) => Dispatch(() =>
    {
        if (_suspended == suspended) return;
        _suspended = suspended;
        if (suspended)
        {
            StopInternal(reason, resume: true);
            EnsureReleased();
            return;
        }

        // A modal close is an input boundary. Do not trust a frame captured while
        // it was visible; the App invalidates its matching capture revision too.
        _frames.RequireNextFrame();
        TryResumeInput();
        Publish(_state.IsEnabled ? "조작 켜짐 · 렌즈 안으로 이동하세요" : "버튼 해제와 최신 화면을 기다린 뒤 조작을 재개합니다");
    });
    private void EnsureReleased()
    {
        if (_state.IsPressed) throw new InvalidOperationException("Windows가 버튼 해제를 수락하지 않았습니다. 중지를 다시 눌러 해제를 재시도하세요.");
    }
    public void RefreshFrame() => _frames.RecordFrame(Environment.TickCount64);
    private bool FrameIsFresh() => _frames.IsFresh(Environment.TickCount64, 750);

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
        if (!InstallHook(out var hookError)) { _ready.SetException(new Win32Exception(hookError, "마우스 중계 설치 실패")); return; }
        var timer = SetTimer(0, 0, 50, 0);
        if (timer == 0)
        {
            UnhookWindowsHookEx(_hook);
            _ready.SetException(new Win32Exception(Marshal.GetLastWin32Error(), "입력 해제 타이머 설치 실패"));
            return;
        }
        _ready.SetResult();
        try
        {
            while (GetMessage(out var msg, 0, 0, 0) > 0)
            {
                _commands.ProcessMessage(msg.Id == 0x113, CheckSession);
            }
        }
        finally
        {
            try { StopInternal("입력 중계 종료"); } catch { }
            KillTimer(0, timer);
            UnhookWindowsHookEx(_hook);
        }
    }

    private nint MouseHook(int code, nint message, nint data)
    {
        _hookLiveness.NoteHookActivity();
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
        var duplicateLeftDown = IsDuplicateLeftButtonDown(kind, previousButtons);
        var intercepted = _intercepting || _draining || (_state.IsEnabled && kind == 0x201 && previousButtons == 0
            && _viewport is { } view && view.Contains(new(mouse.Point.X, mouse.Point.Y)));
        if (intercepted && !_draining) _intercepting = true;
        var passMove = _draining && kind == 0x200;
        var hasPrevious = GetCursorPos(out var previous);
        // Do not call SendInput/SetWindowLong from this callback. Nested input routing
        // can deliver the original button before the hook's suppression result returns.
        if (!duplicateLeftDown)
        {
            _commands.Enqueue(() => ProcessMouse(kind, mouse, intercepted, previous, hasPrevious), leftRelease: kind == 0x202);
            if (!PostThreadMessage(_threadId, CommandMessage, 0, 0)) _inputPostFailed = true;
        }
        return intercepted && !passMove ? 1 : CallNextHookEx(0, code, message, data);
    }

    internal static bool IsDuplicateLeftButtonDown(uint kind, int previousButtons) =>
        kind == 0x201 && (previousButtons & 1) != 0;

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
                if (!_leftHeld && !OtherHeld) { _draining = false; Publish("버튼 해제됨 · 조작 자동 재개 대기"); }
                return;
            }
            if (!_state.IsEnabled || _viewport is not { } view)
                return;
            if (!FrameIsFresh()) { StopInternal("화면 갱신 대기 · 조작 자동 재개 대기", resume: true); return; }
            if (!_relaying)
            {
                // Hover belongs to the actual cursor. Only a new press starts source routing.
                if (kind != 0x201) return;
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
            { StopInternal("경계에서 버튼 해제 · 조작 자동 재개 대기", resume: true); return; }
            var target = view.MapToSource(_logical);
            switch (kind)
            {
                case 0x201:
                    // A duplicate native Down must not begin the same target press twice.
                    // The hook normally filters it, and this keeps a queued edge harmless.
                    if (_state.IsPressed) break;
                    BeginCaptureMonitoring(target);
                    _state.Begin(target);
                    break;
                case 0x200:
                    if (_state.IsPressed) _state.Move(target); else _output.MoveTo(target);
                    break;
                case 0x202:
                    _state.Complete(target);
                    StopInternal("버튼 해제 · 실제 커서 복귀", resume: true);
                    TryResumeInput();
                    return;
                default:
                    StopInternal("이 조작은 아직 직접 전달하지 않습니다 · 버튼 해제 후 자동 재개", resume: true);
                    return;
            }
            Publish(_state.IsPressed ? "드래그 중 · 가장자리로 이동하면 중지" : "조작 켜짐 · 실제 대상 반응을 확인하세요");
        }
        catch (Exception ex)
        {
            try { StopInternal($"입력 실패: {ex.Message}", failed: true); } catch { }
        }
    }

    private void StopInternal(string reason, bool resume = false, bool failed = false)
    {
        var wasRequested = _state.IsRequested;
        var wasPressed = _state.IsPressed;
        var staleFrame = reason == "화면 갱신 대기 · 조작 자동 재개 대기";
        var restore = _relaying;
        var ownsPress = restore || _intercepting || _draining || _state.IsPressed;
        _draining |= ownsPress && (_leftHeld || OtherHeld);
        _intercepting = false;
        try
        {
            if (resume) _state.Pause(ownsPhysicalPress: ownsPress);
            else _state.Stop(ownsPhysicalPress: ownsPress);
        }
        catch (Exception ex) { reason += $" · 해제 재시도 필요: {ex.Message}"; }
        finally
        {
            _relaying = false;
            SetPassthrough(false);
            // Up is sent before this move, preventing a long stroke towards the toolbar.
            if (restore && !_state.IsPressed)
                _output.MoveTo(new ScreenPoint((int)Math.Round(_logical.X), (int)Math.Round(_logical.Y)));
            if (((wasRequested || failed) && !_state.IsRequested) || (staleFrame && wasRequested))
                RecordStop(reason, wasPressed, staleFrame);
            _captureMonitor.Reset();
            Publish(reason);
        }
    }

    private void CheckSession()
    {
        try
        {
            CheckHookLiveness();
            if (_inputPostFailed) { _inputPostFailed = false; StopInternal("입력 큐 전달 실패 · 조작 중지"); return; }
            if (_state.IsPressed && !_state.IsEnabled)
            {
                var wasRequested = _state.IsRequested;
                _state.Stop();
                if (wasRequested) RecordStop("입력 상태 불일치 · 조작 중지", wasPressed: true);
                Publish(_message);
            }
            if (!_state.IsRequested || _suspended) return;
            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) { StopInternal("Esc · 보기로 전환"); return; }
            var desktop = OpenInputDesktop(0, false, 1);
            if (desktop == 0) { StopInternal("입력 화면 변경 · 조작 중지"); return; }
            CloseDesktop(desktop);
            if (!_state.IsEnabled) { TryResumeInput(); return; }
            if (!FrameIsFresh()) { StopInternal("화면 갱신 대기 · 조작 자동 재개 대기", resume: true); return; }
            if (_state.IsPressed && _captureMonitor.LostCapture(ReadTargetCapture, () => _commands.HasPendingLeftRelease))
                StopInternal("원본 대상 capture 손실 또는 조회 실패 · 조작 중지");
        }
        catch (Exception ex) { try { StopInternal($"입력 상태 확인 실패: {ex.Message}", failed: true); } catch { } }
    }

    private void BeginCaptureMonitoring(ScreenPoint point)
    {
        var window = WindowFromPoint(new NativePoint { X = point.X, Y = point.Y });
        var thread = window == 0 ? 0 : GetWindowThreadProcessId(window, out _);
        _captureMonitor.Begin(thread, window == 0 ? 0 : GetAncestor(window, 2));
    }

    private static CaptureSample ReadTargetCapture(uint thread, nint root)
    {
        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        var success = GetGUIThreadInfo(thread, ref info);
        var captureThread = info.Capture == 0 ? 0 : GetWindowThreadProcessId(info.Capture, out _);
        var foreground = GetForegroundWindow();
        // Foreground is corroborating evidence only, never the capture query target.
        // Owned dialogs and helper windows in the source thread remain related.
        var focusMovedAway = foreground != 0
            && GetWindowThreadProcessId(foreground, out _) != thread
            && GetAncestor(foreground, 3) != GetAncestor(root, 3);
        return new(success, info.Capture, info.Capture == 0 ? 0 : GetAncestor(info.Capture, 2),
            captureThread, IsWindow(root), focusMovedAway);
    }

    private void TryResumeInput()
    {
        if (_viewport is null || !FrameIsFresh() || _draining || _intercepting || _suspended) return;
        // Hook-observed release must drain first. Sampling cannot skip a queued physical Up.
        var held = _hookButtons != 0 || _leftHeld || OtherHeld
            || (GetAsyncKeyState(1) & 0x8000) != 0 || ReadOtherButtons() != 0;
        if (_state.TryResume(held)) Publish("조작 켜짐 · 렌즈 안으로 이동하세요");
    }

    private static int ReadOtherButtons() => ((GetAsyncKeyState(2) & 0x8000) != 0 ? 1 : 0)
        | ((GetAsyncKeyState(4) & 0x8000) != 0 ? 2 : 0)
        | ((GetAsyncKeyState(5) & 0x8000) != 0 ? 4 : 0)
        | ((GetAsyncKeyState(6) & 0x8000) != 0 ? 8 : 0);

    // Hook-side layout: left 1, right 2, middle 4, X1 8, X2 16.
    private static int ReadAllButtons() => ((GetAsyncKeyState(1) & 0x8000) != 0 ? 1 : 0) | (ReadOtherButtons() << 1);

    private bool InstallHook(out int error)
    {
        if (_hook != 0) { UnhookWindowsHookEx(_hook); _hook = 0; }
        _hook = SetWindowsHookEx(14, _mouseProc, GetModuleHandle(null), 0);
        error = _hook == 0 ? Marshal.GetLastWin32Error() : 0;
        return _hook != 0;
    }

    private const int MaxAutoReinstalls = 12;

    private void CheckHookLiveness()
    {
        if (_hookReinstallDisabled || !GetCursorPos(out var cursor)) return;
        var buttons = ReadAllButtons();
        var now = Environment.TickCount64;
        var action = _hookLiveness.Sample(now, cursor.X, cursor.Y, buttons);
        // Never disturb a live input session. A reinstall releases the button, so tearing one
        // down mid-drag is the very failure the watchdog must not cause. Physical hold included:
        // suspicion during a press waits until the button is released.
        if (action == HookLivenessMonitor.Action.None
            || _relaying || _intercepting || _draining || _state.IsPressed || _leftHeld || OtherHeld)
            return;

        if (action == HookLivenessMonitor.Action.Probe)
        {
            // A tagged move to the current position is invisible and presses nothing, yet a live
            // hook echoes it back as activity, which clears the suspicion before any reinstall.
            try { _output.MoveTo(new ScreenPoint(cursor.X, cursor.Y)); } catch { }
            return;
        }

        // The self-probe never echoed: the hook is confirmed dropped (LowLevelHooksTimeout).
        // Reinstall on this thread and trust the OS for button state after the outage.
        var strikes = _hookLiveness.Strikes;
        var installed = InstallHook(out var error);
        _hookLiveness.NoteReinstalled(now);
        _hookButtons = buttons;
        _leftHeld = (buttons & 1) != 0;
        _otherButtonsHeld = buttons >> 1;
        _state.ObservePhysicalButton(_leftHeld || OtherHeld);
        if (buttons == 0) _draining = false;
        RecordHookReinstall(installed, error, strikes, false, false, buttons);
        if (!installed) { StopInternal($"입력 훅 재설치 실패 · 조작 중지 (Win32 {error})", failed: true); return; }
        if (++_hookReinstallCount >= MaxAutoReinstalls)
        {
            // A hook that keeps dying after verified reinstalls is a different, persistent fault.
            // Storming past this point only churns the chain; surface it instead of hiding it.
            _hookReinstallDisabled = true;
            RecordHookReinstall(true, 0, strikes, false, false, buttons);
        }
        if (_state.IsRequested) Publish("입력 훅 재설치 · 조작 자동 재개");
    }

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
            _state.IsWaitingForRelease || _draining, _logical, message, _state.IsRequested));
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
