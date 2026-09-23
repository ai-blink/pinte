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
    private readonly object _workerGate = new();
    private TaskCompletionSource _ready = null!;
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;
    private readonly Dictionary<nint, nint> _savedStyles = [];
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
    private long _activeRelayAttempt;
    private readonly HookLivenessMonitor _hookLiveness = new();
    private readonly HookRecoveryLimiter _hookRecovery = new();
    private readonly HookLossRecoveryState _hookLossRecovery = new();
    private readonly HookWorkerLifecycle _workerLifecycle = new();
    private nint _hook, _keyboardHook;
    private bool _physicalEscapeRequested;
    private bool _hookRecoveryExhausted;
    private bool _startWhenWorkerReady;
    private TaskCompletionSource? _replacementReady;
    private string _message = "보기 · 실제 입력 꺼짐";

    public WindowsLivePointerRelay()
    {
        _state = new LensInputState(_output);
        _sequence = new(_state, () => Environment.TickCount64, CursorIsAt, RestoreLogicalCursor, OnSequenceTrace);
        _mouseProc = MouseHook;
        _keyboardProc = KeyboardHook;
        // A starved hook thread trips LowLevelHooksTimeout, after which Windows drops the hook.
        // StartWorker can later replace that thread without discarding the requested session.
        StartWorker();
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
        Task? replacementReady = null;
        await Dispatch(() =>
        {
            // A recovery budget exhaustion means the existing message thread was unable to
            // receive callbacks after successful handle reinstalls. A new lens request must not
            // put another handle onto that known-bad thread; wait for the successor instead.
            if (!_state.IsRequested && _hookRecoveryExhausted)
            {
                _startWhenWorkerReady = true;
                replacementReady = RequestHookWorkerReplacement(0, ReadAllButtons());
                Publish("입력 훅 새 스레드 준비 대기 · 완료 뒤 조작을 자동 시작합니다");
                return;
            }
            if (!_state.IsRequested && !PrepareNewInputSession())
            {
                RecordRelayPath("session-start-failed", "입력 세션 준비 실패");
                return;
            }
            _state.RequestStart();
            TryResumeInput();
            armed = _state.IsEnabled;
            RecordRelayPath(armed ? "session-armed" : "session-waiting",
                armed ? "입력 세션 준비됨" : "입력 세션이 버튼 해제 또는 최신 화면을 기다림");
            Publish(armed ? "조작 켜짐 · 렌즈 안으로 이동하세요" : "버튼 해제와 최신 화면을 기다린 뒤 자동 재개합니다");
        });
        if (replacementReady is not null)
        {
            await replacementReady.ConfigureAwait(false);
            await Dispatch(() => armed = _state.IsEnabled);
        }
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
        TaskCompletionSource ready;
        lock (_workerGate) ready = _ready;
        await ready.Task.ConfigureAwait(false);
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsLivePointerRelay));
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = 1;
        _commands.Enqueue(() =>
        {
            if (Interlocked.Exchange(ref accepted, 0) == 0) return;
            try { action(); done.SetResult(); } catch (Exception ex) { done.SetException(ex); }
        });
        uint threadId;
        bool replacementPending;
        lock (_workerGate)
        {
            threadId = _threadId;
            replacementPending = _workerLifecycle.ReplacementPending;
        }
        if (threadId == 0 || !PostThreadMessage(threadId, CommandMessage, 0, 0))
        {
            // A scheduled successor owns the same queue and drains it from its timer.
            // Do not reject a UI command merely because the old hook worker is exiting.
            if (!replacementPending)
            {
                Interlocked.Exchange(ref accepted, 0);
                throw new InvalidOperationException("입력 스레드에 요청을 전달하지 못했습니다.");
            }
        }
        await done.Task.ConfigureAwait(false);
    }

    private void StartWorker(TaskCompletionSource? ready = null)
    {
        ready ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => Run(ready))
        {
            IsBackground = true,
            Name = $"Magnifier pointer relay #{_workerLifecycle.Generation}",
            Priority = ThreadPriority.Highest
        };
        thread.SetApartmentState(ApartmentState.STA);
        lock (_workerGate)
        {
            _ready = ready;
            _threadId = 0;
        }
        thread.Start();
    }

    private void Run(TaskCompletionSource ready)
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, 0, 0, 0, 0);
        if (!InstallHook(out var hookError))
        {
            StopInternal($"마우스 입력 훅 설치 실패 · 조작 중지 (Win32 {hookError})", failed: true);
            ready.TrySetException(new Win32Exception(hookError, "마우스 중계 설치 실패"));
            return;
        }
        if (!InstallKeyboardHook(out var keyboardError))
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
            StopInternal($"Esc 입력 훅 설치 실패 · 조작 중지 (Win32 {keyboardError})", failed: true);
            ready.TrySetException(new Win32Exception(keyboardError, "Esc 입력 훅 설치 실패"));
            return;
        }
        var timer = SetTimer(0, 0, 50, 0);
        if (timer == 0)
        {
            if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
            UnhookWindowsHookEx(_hook);
            _keyboardHook = 0;
            _hook = 0;
            var error = Marshal.GetLastWin32Error();
            StopInternal($"입력 상태 타이머 설치 실패 · 조작 중지 (Win32 {error})", failed: true);
            ready.TrySetException(new Win32Exception(error, "입력 해제 타이머 설치 실패"));
            return;
        }
        _hookLiveness.NoteReinstalled(Environment.TickCount64);
        _hookLossRecovery.Reset();
        SynchronizeObservedButtons(ReadAllButtons());
        if (_startWhenWorkerReady)
        {
            _startWhenWorkerReady = false;
            _hookRecoveryExhausted = false;
            _hookRecovery.Reset();
            _state.RequestStart();
            TryResumeInput();
            RecordRelayPath(_state.IsEnabled ? "session-armed" : "session-waiting",
                _state.IsEnabled ? "새 입력 훅 스레드에서 입력 세션 준비됨" : "새 입력 훅 스레드가 버튼 해제를 기다림");
        }
        ready.TrySetResult();
        if (_state.IsRequested) Publish("입력 훅 스레드 준비됨 · 조작 자동 재개");
        try
        {
            while (GetMessage(out var msg, 0, 0, 0) > 0)
            {
                ProcessWorkerMessage(msg, timer);
            }
        }
        finally
        {
            CancelSequenceWake();
            KillTimer(0, timer);
            if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
            UnhookWindowsHookEx(_hook);
            _keyboardHook = 0;
            _hook = 0;
            TaskCompletionSource? replacement = null;
            lock (_workerGate)
            {
                if (!_disposed && _workerLifecycle.ReplacementPending)
                {
                    replacement = _replacementReady;
                    _replacementReady = null;
                    _workerLifecycle.CompleteReplacement();
                }
            }
            if (replacement is not null)
            {
                RecordHookWorkerRestart("입력 훅 스레드 교체 시작");
                StartWorker(replacement);
            }
            else
            {
                try { StopInternal("입력 중계 종료"); } catch { }
            }
        }
    }

    private void StopInternal(string reason, bool resume = false, bool failed = false)
    {
        _sequence.CancelPending();
        CancelSequenceWake();
        _hookLossRecovery.Reset();
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
            {
                _output.MoveTo(new ScreenPoint((int)Math.Round(_logical.X), (int)Math.Round(_logical.Y)));
                RecordRelayPath("cursor-restored", reason, _activeRelayAttempt);
            }
            _activeRelayAttempt = 0;
            if (((wasRequested || failed) && !_state.IsRequested) || (staleFrame && wasRequested))
                RecordStop(reason, wasPressed, staleFrame);
            _captureMonitor.Reset();
            Publish(reason);
        }
    }

    private void CheckSession() => CheckSession(monitor: true);

    private void CheckSession(bool monitor)
    {
        try
        {
            if (_physicalEscapeRequested)
            {
                _physicalEscapeRequested = false;
                if (_state.IsRequested) { StopInternal("Esc · 보기로 전환"); return; }
            }
            if (monitor) CheckHookLiveness();
            if (_inputPostFailed) { _inputPostFailed = false; StopInternal("입력 큐 전달 실패 · 조작 중지"); return; }
            if (_state.IsPressed && !_state.IsEnabled)
            {
                var wasRequested = _state.IsRequested;
                _state.Stop();
                if (wasRequested) RecordStop("입력 상태 불일치 · 조작 중지", wasPressed: true);
                Publish(_message);
            }
            if (!_state.IsRequested || _suspended) return;
            var desktop = OpenInputDesktop(0, false, 1);
            if (desktop == 0) { StopInternal("입력 화면 변경 · 조작 중지"); return; }
            CloseDesktop(desktop);
            if (!_state.IsEnabled) { TryResumeInput(); return; }
            if (!FrameIsFresh()) { StopInternal("화면 갱신 대기 · 조작 자동 재개 대기", resume: true); return; }
            AdvancePointerSequence();
            if (monitor && _state.IsPressed && _captureMonitor.LostCapture(ReadTargetCapture,
                () => _commands.HasPendingLeftRelease || _sequence.HasPendingRelease))
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
        if (_state.TryResume(held))
        {
            RecordRelayPath("session-rearmed", "입력 세션 자동 재개");
            Publish("조작 켜짐 · 렌즈 안으로 이동하세요");
        }
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
