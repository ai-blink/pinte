using Magnifier.Core;

namespace Magnifier.Infrastructure;

internal sealed record PointerTiming(int ArrivalMs = 100, int MinimumHoldMs = 35,
    int PostReleaseMs = 60, int BetweenGesturesMs = 0);

// One relay worker owns the FIFO and clock. No sleeps or delayed continuations can
// outlive CancelPending. Physical input is collected independently of output timing.
internal sealed class TimedPointerSequence
{
    internal enum Stage { Idle, Arriving, Pressed, Recovering, BetweenGestures }
    internal enum Trace { Prepared, BeforePress, Pressed, Released, Restored }
    private sealed class Gesture(ScreenPoint start, long attempt)
    {
        public ScreenPoint Start { get; } = start;
        public long Attempt { get; } = attempt;
        public Queue<ScreenPoint> Moves { get; } = new();
        public ScreenPoint? End { get; set; }
    }

    internal const int MaximumGestures = 8;
    internal const int MaximumBufferedMoves = 512;
    private readonly LensInputState _state;
    private readonly Func<long> _clock;
    private readonly Func<ScreenPoint, bool> _cursorAt;
    private readonly Action _restoreCursor;
    private readonly Action<Trace, ScreenPoint, long> _trace;
    private readonly Queue<Gesture> _waiting = new();
    private Gesture? _current, _collecting;
    private long _dueAt;
    private int _bufferedMoves;

    public TimedPointerSequence(LensInputState state, Func<long> clock,
        Func<ScreenPoint, bool> cursorAt, Action restoreCursor,
        Action<Trace, ScreenPoint, long> trace, PointerTiming? timing = null)
    {
        _state = state;
        _clock = clock;
        _cursorAt = cursorAt;
        _restoreCursor = restoreCursor;
        _trace = trace;
        Timing = timing ?? new();
        if (Timing.ArrivalMs < 0 || Timing.MinimumHoldMs < 0 ||
            Timing.PostReleaseMs < 0 || Timing.BetweenGesturesMs < 0)
            throw new ArgumentOutOfRangeException(nameof(timing));
    }

    public PointerTiming Timing { get; }
    public Stage Phase { get; private set; }
    public bool IsBusy => Phase != Stage.Idle || _waiting.Count != 0;
    public int PendingGestures => _waiting.Count + (_current is null ? 0 : 1);
    public bool HasPendingRelease => Phase == Stage.Pressed && _current?.End is not null;
    // The owner schedules a wake only for executable work. A held pointer with no
    // queued movement waits for real input, not a busy polling timer.
    public long? NextWakeAt => Phase switch
    {
        Stage.Idle => _waiting.Count == 0 ? null : _clock(),
        Stage.Pressed when _current!.Moves.Count != 0 => _clock(),
        Stage.Pressed => _current!.End is null ? null : _dueAt,
        _ => _dueAt
    };

    public void Begin(ScreenPoint point, long attempt)
    {
        if (_collecting is not null) throw new InvalidOperationException("이전 버튼의 해제를 기다립니다.");
        if (PendingGestures >= MaximumGestures)
            throw new InvalidOperationException("입력 대기열이 가득 차 조작을 중지합니다.");
        _collecting = new(point, attempt);
        _waiting.Enqueue(_collecting);
    }

    public void Move(ScreenPoint point)
    {
        if (_collecting is null) return; // Hover only updates the logical lens cursor.
        if (_bufferedMoves >= MaximumBufferedMoves)
            throw new InvalidOperationException("드래그 입력 대기열이 가득 차 조작을 중지합니다.");
        _collecting.Moves.Enqueue(point);
        _bufferedMoves++;
    }

    public void End(ScreenPoint point)
    {
        if (_collecting is null) return; // Duplicate release must not create another click.
        _collecting.End = point;
        _collecting = null;
    }

    // Call before the owner's Pause/Stop, which retains responsibility for any failed Up.
    public void CancelPending()
    {
        _waiting.Clear();
        _current = _collecting = null;
        _bufferedMoves = 0;
        _dueAt = 0;
        Phase = Stage.Idle;
    }

    public void Tick()
    {
        if (!_state.IsEnabled || !_state.IsRequested)
        {
            CancelPending();
            return;
        }
        try { Advance(); }
        catch { CancelPending(); throw; }
    }

    private void Advance()
    {
        var moveBudget = 8;
        // Continue through zero/expired waits without an artificial timer round-trip.
        // Both transitions and moves stay bounded even when every setting is zero.
        for (var transition = 0; transition < MaximumGestures * 5; transition++)
        {
            switch (Phase)
            {
                case Stage.Idle:
                    if (!_waiting.TryDequeue(out _current)) return;
                    if (!_state.PrepareBegin(_current.Start))
                        throw new InvalidOperationException("원본 위치 준비가 취소되었습니다.");
                    _dueAt = _clock() + Timing.ArrivalMs;
                    Phase = Stage.Arriving;
                    _trace(Trace.Prepared, _current.Start, _current.Attempt);
                    break;
                case Stage.Arriving:
                    if (_clock() < _dueAt) return;
                    // Never re-move and erase the arrival interval, or press after drift.
                    if (!_cursorAt(_current!.Start))
                        throw new InvalidOperationException("도착 대기 중 실제 커서 위치가 변경되어 입력을 중지합니다.");
                    _trace(Trace.BeforePress, _current.Start, _current.Attempt);
                    if (!_state.BeginPrepared()) throw new InvalidOperationException("대상 누름이 취소되었습니다.");
                    _dueAt = _clock() + Timing.MinimumHoldMs;
                    Phase = Stage.Pressed;
                    _trace(Trace.Pressed, _current.Start, _current.Attempt);
                    break;
                case Stage.Pressed:
                    var gesture = _current!;
                    while (moveBudget > 0 && gesture.Moves.TryDequeue(out var point))
                    {
                        moveBudget--;
                        _bufferedMoves--;
                        _state.Move(point);
                    }
                    if (gesture.End is not { } end || gesture.Moves.Count != 0 || _clock() < _dueAt) return;
                    _state.Complete(end);
                    _dueAt = _clock() + Timing.PostReleaseMs;
                    Phase = Stage.Recovering;
                    _trace(Trace.Released, end, gesture.Attempt);
                    break;
                case Stage.Recovering:
                    if (_clock() < _dueAt) return;
                    _restoreCursor(); // Only after successful Up.
                    _dueAt = _clock() + Timing.BetweenGesturesMs;
                    var completed = _current!;
                    _current = null;
                    Phase = Stage.BetweenGestures;
                    _trace(Trace.Restored, completed.End!.Value, completed.Attempt);
                    break;
                case Stage.BetweenGestures:
                    if (_clock() < _dueAt) return;
                    Phase = Stage.Idle;
                    break;
            }
        }
    }
}
