using System.Text.Json;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    private static readonly object StopLogLock = new();
    private long _stopSequence;

    private void RecordStop(string reason, bool wasPressed, bool staleFrame = false)
    {
        // No file IO on the hook/message thread. A logging failure must never affect release.
        var json = JsonSerializer.Serialize(new
        {
            Sequence = ++_stopSequence,
            At = DateTimeOffset.UtcNow,
            EngineBuild = typeof(WindowsLivePointerRelay).Assembly.ManifestModule.ModuleVersionId,
            Reason = reason,
            StaleFrame = staleFrame,
            WasPressed = wasPressed,
            IsPressed = _state.IsPressed,
            InputRequested = _state.IsRequested,
            InputEnabled = _state.IsEnabled,
            IsSuspended = _suspended,
            IsDraining = _draining,
            IsIntercepting = _intercepting,
            FrameIsFresh = FrameIsFresh(),
            PhysicalLeftHeld = _leftHeld,
            HookButtons = _hookButtons,
            PendingLeftRelease = _commands.HasPendingLeftRelease,
            Capture = _captureMonitor.Trace
        });
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
