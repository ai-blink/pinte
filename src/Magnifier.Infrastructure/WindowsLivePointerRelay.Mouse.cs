using System.Runtime.InteropServices;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    internal enum MouseInputOrigin { Physical, Assistive, RelayOutput }

    // INJECTED describes the mechanism, not the producer. Other accessibility tools
    // must keep their normal lens routing even when their flags match our own output.
    internal static MouseInputOrigin ClassifyMouseInput(uint flags, nint extraInfo) =>
        extraInfo == WindowsPointerInput.InjectionTag ? MouseInputOrigin.RelayOutput :
        (flags & 1) != 0 ? MouseInputOrigin.Assistive : MouseInputOrigin.Physical;

    internal static MouseInputOrigin ObserveMouseButtons(uint kind, uint flags, nint extraInfo,
        uint mouseData, ref int buttons)
    {
        var origin = ClassifyMouseInput(flags, extraInfo);
        if (origin == MouseInputOrigin.RelayOutput) return origin;
        var bit = kind switch
        {
            0x201 or 0x202 => 1,
            0x204 or 0x205 => 2,
            0x207 or 0x208 => 4,
            0x20B or 0x20C => (mouseData >> 16) == 1 ? 8 : 16,
            _ => 0
        };
        if (kind is 0x201 or 0x204 or 0x207 or 0x20B) buttons |= bit;
        if (kind is 0x202 or 0x205 or 0x208 or 0x20C) buttons &= ~bit;
        return origin;
    }

    private nint MouseHook(int code, nint message, nint data)
    {
        _hookLiveness.NoteHookActivity();
        if (code < 0) return CallNextHookEx(0, code, message, data);
        var mouse = Marshal.PtrToStructure<MouseHookData>(data);
        var kind = (uint)message;
        var previousButtons = _hookButtons;
        if (ObserveMouseButtons(kind, mouse.Flags, mouse.ExtraInfo, mouse.MouseData, ref _hookButtons)
            == MouseInputOrigin.RelayOutput)
            return CallNextHookEx(0, code, message, data);
        var duplicateLeftDown = IsDuplicateLeftButtonDown(kind, previousButtons);
        var relayAttempt = kind == 0x201 && !duplicateLeftDown && _state.IsEnabled && previousButtons == 0
            && _viewport is { } candidateView && (candidateView.Contains(new(mouse.Point.X, mouse.Point.Y))
                || (_relaying && candidateView.Contains(new((int)_logical.X, (int)_logical.Y))))
            ? NextRelayAttempt() : 0;
        var intercepted = _intercepting || _draining || (_state.IsEnabled && kind == 0x201 && previousButtons == 0
            && _viewport is { } view && view.Contains(new(mouse.Point.X, mouse.Point.Y)));
        if (intercepted && !_draining) _intercepting = true;
        var passMove = _draining && kind == 0x200;
        var hasPrevious = GetCursorPos(out var previous);
        // Do not call SendInput/SetWindowLong from this callback. Nested input routing
        // can deliver the original button before the hook's suppression result returns.
        if (!duplicateLeftDown)
        {
            _commands.Enqueue(() => ProcessMouse(kind, mouse, intercepted, previous, hasPrevious, relayAttempt), leftRelease: kind == 0x202);
            var commandPosted = PostThreadMessage(_threadId, CommandMessage, 0, 0);
            if (!commandPosted) _inputPostFailed = true;
            if (relayAttempt != 0 || !commandPosted)
                RecordRelayPath(commandPosted ? "hook-left-down-enqueued" : "hook-command-post-failed",
                    commandPosted ? "렌즈 안 왼쪽 버튼을 명령 큐에 전달" : "입력 명령 큐 전달 실패", relayAttempt,
                    commandPosted, kind, mouse.Point.X, mouse.Point.Y);
        }
        return intercepted && !passMove ? 1 : CallNextHookEx(0, code, message, data);
    }

    internal static bool IsDuplicateLeftButtonDown(uint kind, int previousButtons) =>
        kind == 0x201 && (previousButtons & 1) != 0;

    private void ProcessMouse(uint kind, MouseHookData mouse, bool intercepted, NativePoint previous, bool hasPrevious,
        long relayAttempt = 0)
    {
        if (relayAttempt != 0)
            RecordRelayPath("command-dequeued", "렌즈 입력 명령을 처리 시작", relayAttempt, pointerMessage: kind,
                pointerX: mouse.Point.X, pointerY: mouse.Point.Y);
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
        if (!intercepted)
        {
            if (relayAttempt != 0) RecordRelayPath("command-rejected", "입력 가로채기 상태가 해제됨", relayAttempt);
            return;
        }
        try
        {
            if (_draining)
            {
                // The held button belongs to the old drag, never to a return button.
                if (!_leftHeld && !OtherHeld) { _draining = false; Publish("버튼 해제됨 · 조작 자동 재개 대기"); }
                if (relayAttempt != 0) RecordRelayPath("command-rejected", "이전 입력 버튼 해제 대기", relayAttempt);
                return;
            }
            if (!_state.IsEnabled || _viewport is not { } view)
            {
                if (relayAttempt != 0) RecordRelayPath("command-rejected", "입력 세션 또는 렌즈 영역이 준비되지 않음", relayAttempt);
                return;
            }
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
            }
            else if (kind == 0x200)
            {
                if (ClassifyMouseInput(mouse.Flags, mouse.ExtraInfo) == MouseInputOrigin.Assistive)
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
                    // Physical gestures and output presses have independent lifetimes.
                    // An earlier output still being held must not discard a later click.
                    var attempt = relayAttempt != 0 ? relayAttempt : NextRelayAttempt();
                    _sequence.Begin(target, attempt);
                    RecordRelayPath("gesture-queued", "렌즈 누름을 입력 순서에 추가", attempt,
                        pointerX: target.X, pointerY: target.Y);
                    break;
                case 0x200:
                    _sequence.Move(target);
                    break;
                case 0x202:
                    _sequence.End(target);
                    break;
                default:
                    StopInternal("이 조작은 아직 직접 전달하지 않습니다 · 버튼 해제 후 자동 재개", resume: true);
                    return;
            }
            AdvancePointerSequence();
        }
        catch (Exception ex)
        {
            try { StopInternal($"입력 실패: {ex.Message}", failed: true); } catch { }
        }
    }

}
