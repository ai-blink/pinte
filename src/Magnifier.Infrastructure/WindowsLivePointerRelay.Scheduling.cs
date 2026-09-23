using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    private const uint SequenceMessage = 0x8002;
    private nuint _sequenceTimer;
    private long? _sequenceTimerDue;
    private bool _sequenceWakePosted;

    private void ScheduleSequenceWake()
    {
        if (_sequence.NextWakeAt is not { } due)
        {
            CancelSequenceTimer();
            return;
        }
        var remaining = due - Environment.TickCount64;
        if (remaining <= 0)
        {
            CancelSequenceTimer();
            if (_sequenceWakePosted) return;
            if (!PostThreadMessage(_threadId, SequenceMessage, 0, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "입력 후속 처리 예약 실패");
            _sequenceWakePosted = true;
            return;
        }
        if (_sequenceTimer != 0 && _sequenceTimerDue == due) return;
        CancelSequenceTimer();
        // Native message timer only: no sleeping worker or off-thread input. Windows
        // clamps short intervals to at least 10ms; a wake can still be delivered late.
        _sequenceTimer = SetTimer(0, 0, (uint)Math.Clamp(remaining, 10, int.MaxValue), 0);
        if (_sequenceTimer == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "입력 시퀀스 타이머 설치 실패");
        _sequenceTimerDue = due;
    }

    private void CancelSequenceTimer()
    {
        if (_sequenceTimer != 0) KillTimer(0, _sequenceTimer);
        _sequenceTimer = 0;
        _sequenceTimerDue = null;
    }

    private void CancelSequenceWake()
    {
        CancelSequenceTimer();
        _sequenceWakePosted = false;
    }

    private void ProcessWorkerMessage(Message message, nuint healthTimer)
    {
        var sequenceWake = message.Id == SequenceMessage ||
            (message.Id == 0x113 && _sequenceTimer != 0 && message.WParam == _sequenceTimer);
        if (sequenceWake)
        {
            if (message.Id == SequenceMessage) _sequenceWakePosted = false;
            CancelSequenceTimer();
        }
        // Preserve the watchdog's existing 50ms sampling/grace periods. Drain user
        // commands (especially Stop and Up) before a queued sequence continuation.
        var healthWake = message.Id == 0x113 && message.WParam == healthTimer;
        _commands.ProcessMessage(healthWake, CheckSession);
        // A zero-wait Up can finish the sequence inside a queued command while
        // HasPendingLeftRelease is still true. Finalize once after that flag drains;
        // do not poll/spin if a physical button is still held.
        if (sequenceWake || (!healthWake && _relaying && !_sequence.IsBusy))
            CheckSession(monitor: false);
    }
}
