using System.Text.Json;
using System.Text.Json.Serialization;
using Magnifier.Core;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    private static readonly object StopLogLock = new();
    private static readonly JsonSerializerOptions DiagnosticJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private long _stopSequence;
    private long _relayAttemptSequence;

    internal sealed record DiagnosticRecord(string Event, long Sequence, DateTimeOffset At, Guid EngineBuild,
        string Reason, bool StaleFrame, bool WasPressed, bool IsPressed, bool InputRequested, bool InputEnabled,
        bool IsSuspended, bool IsDraining, bool IsIntercepting, bool FrameIsFresh, bool PhysicalLeftHeld,
        int HookButtons, bool PendingLeftRelease, CaptureTrace Capture, bool? HookInstalled = null,
        int? Win32Error = null, int? Strikes = null, bool? MissedRelease = null, int? OsButtons = null,
        int? RecoveryAttempts = null, int? RecoveryMaximumAttempts = null, string? RecoveryAction = null,
        string? RelayStage = null, long? RelayAttempt = null, bool? CommandPosted = null,
        uint? PointerMessage = null, int? PointerX = null, int? PointerY = null,
        DeliveryTrace? Delivery = null);

    internal sealed record DeliveryTrace(bool CursorReadSucceeded, int CursorX, int CursorY,
        long WindowAtPoint, long RootAtPoint, uint ProcessAtPoint, long RootStyle, long Foreground);

    // These are sender-side snapshots, not acknowledgements from the target application.
    private void RecordDelivery(string stage, string reason, ScreenPoint target, uint message)
    {
        var cursorRead = GetCursorPos(out var cursor);
        var window = WindowFromPoint(new NativePoint { X = target.X, Y = target.Y });
        var root = window == 0 ? 0 : GetAncestor(window, 2);
        uint process = 0;
        if (window != 0) GetWindowThreadProcessId(window, out process);
        Append(Snapshot("relay-path", reason, _state.IsPressed, false) with
        {
            RelayStage = stage, RelayAttempt = _activeRelayAttempt,
            PointerMessage = message, PointerX = target.X, PointerY = target.Y,
            Delivery = new(cursorRead, cursor.X, cursor.Y, (long)window, (long)root, process,
                root == 0 ? 0 : (long)GetWindowLongPtr(root, ExtendedStyle), (long)GetForegroundWindow())
        });
    }

    private DiagnosticRecord Snapshot(string kind, string reason, bool wasPressed, bool staleFrame) => new(kind,
        Interlocked.Increment(ref _stopSequence), DateTimeOffset.UtcNow, typeof(WindowsLivePointerRelay).Assembly.ManifestModule.ModuleVersionId,
        reason, staleFrame, wasPressed, _state.IsPressed, _state.IsRequested, _state.IsEnabled, _suspended, _draining,
        _intercepting, FrameIsFresh(), _leftHeld, _hookButtons, _commands.HasPendingLeftRelease, _captureMonitor.Trace);

    private void RecordStop(string reason, bool wasPressed, bool staleFrame = false) =>
        Append(Snapshot("stop", reason, wasPressed, staleFrame));

    private void RecordHookWorkerRestart(string reason, int? strikes = null, int? osButtons = null) =>
        Append(Snapshot("hook-worker-restart", reason, wasPressed: _state.IsPressed, staleFrame: false) with
        {
            HookInstalled = _hook != 0,
            Strikes = strikes,
            OsButtons = osButtons,
            RecoveryAttempts = _hookRecovery.Attempts,
            RecoveryMaximumAttempts = _hookRecovery.MaximumAttempts,
            RecoveryAction = _workerLifecycle.ReplacementPending ? "replace-worker" : "worker-started"
        });

    private void RecordHookLoss(string reason, int osButtons, string action) =>
        Append(Snapshot("hook-loss", reason, wasPressed: _state.IsPressed, staleFrame: false) with
        {
            OsButtons = osButtons, RecoveryAttempts = _hookRecovery.Attempts,
            RecoveryMaximumAttempts = _hookRecovery.MaximumAttempts, RecoveryAction = action
        });

    private void RecordRecoveryExhausted(int osButtons) =>
        Append(Snapshot("hook-recovery-exhausted", "입력 훅 자동 복구 예산 소진", wasPressed: _state.IsPressed, staleFrame: false) with
        {
            OsButtons = osButtons, RecoveryAttempts = _hookRecovery.Attempts,
            RecoveryMaximumAttempts = _hookRecovery.MaximumAttempts, RecoveryAction = "stop"
        });

    private long NextRelayAttempt() => Interlocked.Increment(ref _relayAttemptSequence);

    // Trace dispatch, output and cursor restoration without logging normal pointer moves.
    private void RecordRelayPath(string stage, string reason, long attempt = 0, bool? commandPosted = null,
        uint? pointerMessage = null, int? pointerX = null, int? pointerY = null) =>
        Append(Snapshot("relay-path", reason, wasPressed: _state.IsPressed, staleFrame: false) with
        {
            RelayStage = stage, RelayAttempt = attempt == 0 ? null : attempt, CommandPosted = commandPosted,
            PointerMessage = pointerMessage, PointerX = pointerX, PointerY = pointerY
        });

    private static void Append(DiagnosticRecord record)
    {
        // No file IO on the hook/message thread. A logging failure must never affect release.
        var json = JsonSerializer.Serialize(record, DiagnosticJson);
        _ = Task.Run(() =>
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Magnifier", "diagnostics");
                lock (StopLogLock)
                {
                    Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, $"relay-stop-{Environment.ProcessId}.jsonl"), json + Environment.NewLine);
                }
            }
            catch { /* Diagnostic output cannot cancel or rearm the input session. */ }
        });
    }
}
