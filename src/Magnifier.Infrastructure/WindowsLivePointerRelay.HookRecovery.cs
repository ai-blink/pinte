using System.ComponentModel;
using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
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

    private void CheckHookLiveness()
    {
        if (_hookRecoveryExhausted || !GetCursorPos(out var cursor)) return;
        var buttons = ReadAllButtons();
        var now = Environment.TickCount64;
        var relayOwnsInput = _relaying || _intercepting || _draining || _state.IsPressed;
        var pending = _hookLossRecovery.Decide(HookLivenessMonitor.Action.None, relayOwnsInput, buttons);
        if (pending != HookLossRecoveryState.Action.None)
        {
            ApplyHookRecoveryAction(pending, now, buttons);
            return;
        }

        var action = _hookLiveness.Sample(now, cursor.X, cursor.Y, buttons);
        if (action == HookLivenessMonitor.Action.None) return;

        if (action == HookLivenessMonitor.Action.Probe)
        {
            // A current OS button state is authoritative even when a dead hook left the cached
            // callback state stale. Never inject a probe into a held physical press.
            if (relayOwnsInput || buttons != 0) return;
            // A tagged move to the current position is invisible and presses nothing, yet a live
            // hook echoes it back as activity, which clears the suspicion before any reinstall.
            try { _output.MoveTo(new ScreenPoint(cursor.X, cursor.Y)); } catch { }
            return;
        }

        // The self-probe never echoed: the hook is confirmed dropped. A held press waits for its
        // real OS release; a relayed press then receives exactly one synthetic Up before stopping.
        var recovery = _hookLossRecovery.Decide(action, relayOwnsInput, buttons);
        RecordHookLoss("입력 훅 응답 없음 확인", buttons, recovery.ToString());
        ApplyHookRecoveryAction(recovery, now, buttons);
    }

    private void ApplyHookRecoveryAction(HookLossRecoveryState.Action action, long now, int buttons)
    {
        switch (action)
        {
            case HookLossRecoveryState.Action.WaitForPhysicalRelease:
                Publish("입력 훅 응답 없음 · 버튼 해제 대기");
                return;
            case HookLossRecoveryState.Action.StopAfterPhysicalRelease:
                SynchronizeObservedButtons(buttons);
                _hookRecoveryExhausted = true;
                StopInternal("입력 훅 응답 없음 · 버튼 해제 후 조작 중지", failed: true);
                return;
            case HookLossRecoveryState.Action.Reinstall:
                break;
            default:
                return;
        }

        var strikes = _hookLiveness.Strikes;
        // A short continuous outage may retry. Once its budget is exhausted, stop visibly and
        // remain quiet until the user explicitly opens a new lens session.
        if (!_hookRecovery.TryAcquire(now))
        {
            _hookRecoveryExhausted = true;
            RecordRecoveryExhausted(buttons);
            StopInternal("입력 훅 복구 시도가 60초 이상 끊기지 않고 반복되어 조작을 중지했습니다", failed: true);
            return;
        }
        // Windows binds a low-level hook to this message thread. The log showed a non-zero
        // SetWindowsHookEx result followed by another silent callback stream, so replacing only
        // the handle here is not recovery. Exit this worker and let its finally create a fresh
        // STA message thread after all old handles have been unhooked.
        RequestHookWorkerReplacement(strikes, buttons);
    }

    private bool PrepareNewInputSession()
    {
        _hookRecovery.Reset();
        _hookLossRecovery.Reset();
        if (!_hookRecoveryExhausted) return true;
        _hookRecoveryExhausted = false;
        if (!InstallHook(out var error))
        {
            _hookRecoveryExhausted = true;
            Publish($"입력 훅 재설치 실패 · 조작 중지 (Win32 {error})");
            return false;
        }

        _hookLiveness.NoteReinstalled(Environment.TickCount64);
        SynchronizeObservedButtons(ReadAllButtons());
        return true;
    }

    private Task? RequestHookWorkerReplacement(int strikes, int buttons)
    {
        lock (_workerGate)
        {
            if (_disposed) return null;
            if (_workerLifecycle.ReplacementPending) return _replacementReady?.Task;
            if (!_workerLifecycle.TryBeginReplacement()) return null;
            _replacementReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ready = _replacementReady;
            _threadId = 0;
        }
        RecordHookWorkerRestart("입력 훅 응답 없음 · 새 메시지 스레드 요청", strikes, buttons);
        Publish("입력 훅 응답 없음 · 새 입력 스레드로 복구합니다");
        PostQuitMessage(0);
        return _replacementReady.Task;
    }

    private void SynchronizeObservedButtons(int buttons)
    {
        _hookButtons = buttons;
        _leftHeld = (buttons & 1) != 0;
        _otherButtonsHeld = buttons >> 1;
        _state.ObservePhysicalButton(_leftHeld || OtherHeld);
    }
}
