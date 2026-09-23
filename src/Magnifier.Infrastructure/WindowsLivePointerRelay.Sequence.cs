using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    private readonly TimedPointerSequence _sequence;

    private bool CursorIsAt(ScreenPoint target) => GetCursorPos(out var cursor)
        && Math.Abs((long)cursor.X - target.X) <= 1 && Math.Abs((long)cursor.Y - target.Y) <= 1;

    private void RestoreLogicalCursor() =>
        _output.MoveTo(new((int)Math.Round(_logical.X), (int)Math.Round(_logical.Y)));

    private void OnSequenceTrace(TimedPointerSequence.Trace stage, ScreenPoint point, long attempt)
    {
        _activeRelayAttempt = attempt;
        switch (stage)
        {
            case TimedPointerSequence.Trace.Prepared:
                RecordDelivery("target-prepared", $"원본 도착 · {_sequence.Timing.ArrivalMs}ms 대기", point, 0x200);
                break;
            case TimedPointerSequence.Trace.BeforePress:
                BeginCaptureMonitoring(point);
                break;
            case TimedPointerSequence.Trace.Pressed:
                RecordDelivery("target-press-begun", "Windows 누름 수락 · 대상 반응 미확인", point, 0x201);
                break;
            case TimedPointerSequence.Trace.Released:
                RecordDelivery("target-release-sent", $"Windows 해제 수락 · {_sequence.Timing.PostReleaseMs}ms 체류", point, 0x202);
                _captureMonitor.Reset();
                break;
            case TimedPointerSequence.Trace.Restored:
                RecordRelayPath("cursor-restored", "입력 순서에 따른 커서 복귀", attempt,
                    pointerX: (int)Math.Round(_logical.X), pointerY: (int)Math.Round(_logical.Y));
                break;
        }
    }

    private void AdvancePointerSequence()
    {
        _sequence.Tick();
        if (!_sequence.IsBusy && _relaying && !_state.IsPressed &&
            _hookButtons == 0 && !_leftHeld && !OtherHeld && !_commands.HasPendingLeftRelease)
        {
            // Hover can advance the logical pointer during the final inter-gesture gap.
            // Reconcile that movement before hiding the virtual pointer and returning control.
            if (!CursorIsAt(new((int)Math.Round(_logical.X), (int)Math.Round(_logical.Y))))
                RestoreLogicalCursor();
            _relaying = false;
            StopInternal("입력 순서 완료 · 조작 자동 재개", resume: true);
            TryResumeInput();
            return;
        }
        if (_relaying) Publish(_sequence.Phase switch
        {
            TimedPointerSequence.Stage.Arriving => "원본 도착 · 누르기 전 대기",
            TimedPointerSequence.Stage.Pressed => "입력 중 · 가장자리로 이동하면 중지",
            TimedPointerSequence.Stage.Recovering => "버튼 해제됨 · 원본 위치 유지",
            TimedPointerSequence.Stage.BetweenGestures => "커서 복귀 · 다음 입력 대기",
            _ => "입력 대기"
        });
        ScheduleSequenceWake();
    }
}
