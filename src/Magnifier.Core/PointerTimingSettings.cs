namespace Magnifier.Core;

// User-tunable output timing for one relayed gesture. Each value is a minimum wait
// on the relay worker's monotonic clock; Windows timers can deliver later, never earlier.
// Arrival: cursor moved to source -> Down. Hold: Down -> earliest Up.
// PostRelease: Up -> cursor restored. BetweenGestures: restored -> next queued gesture.
public sealed record PointerTimingSettings(int ArrivalMs = 100, int MinimumHoldMs = 35,
    int PostReleaseMs = 60, int BetweenGesturesMs = 0)
{
    public const int Step = 5;
    public const int MaximumArrivalMs = 300;
    public const int MaximumHoldMs = 200;
    public const int MaximumPostReleaseMs = 300;
    public const int MaximumBetweenGesturesMs = 300;

    public static PointerTimingSettings Default { get; } = new();

    // Starting points only; the right value depends on the machine and target app.
    // "저지연" is the value one user confirmed in one game (2026-09-24), not a universal default.
    public static IReadOnlyList<(string Name, PointerTimingSettings Value)> Presets { get; } =
    [
        ("기본", Default),
        ("빠름", new(50, 20, 30, 0)),
        ("저지연", new(5, 1, 5, 0)),
        ("즉시", new(0, 0, 0, 0))
    ];

    // Fine 1ms steps below 10ms where small values matter; 5ms steps above.
    public static int StepFrom(int value, int direction) =>
        (direction > 0 ? value : value - 1) < 10 ? direction : direction * Step;

    public bool IsValid() => ArrivalMs is >= 0 and <= MaximumArrivalMs
        && MinimumHoldMs is >= 0 and <= MaximumHoldMs
        && PostReleaseMs is >= 0 and <= MaximumPostReleaseMs
        && BetweenGesturesMs is >= 0 and <= MaximumBetweenGesturesMs;

    public override string ToString() => $"{ArrivalMs}/{MinimumHoldMs}/{PostReleaseMs}/{BetweenGesturesMs}ms";
}

// Applied is what the relay worker currently uses for newly started gestures.
// Pending is waiting for the sequence to become idle; queued gestures never mix settings.
public sealed record PointerTimingStatus(PointerTimingSettings Applied, long AppliedRevision,
    PointerTimingSettings? Pending, long PendingRevision);
