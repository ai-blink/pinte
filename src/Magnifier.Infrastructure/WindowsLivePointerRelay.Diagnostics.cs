using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    private static readonly object StopLogLock = new();
    private static readonly JsonSerializerOptions DiagnosticJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private long _stopSequence;

    internal sealed record DiagnosticRecord(string Event, long Sequence, DateTimeOffset At, Guid EngineBuild,
        string Reason, bool StaleFrame, bool WasPressed, bool IsPressed, bool InputRequested, bool InputEnabled,
        bool IsSuspended, bool IsDraining, bool IsIntercepting, bool FrameIsFresh, bool PhysicalLeftHeld,
        int HookButtons, bool PendingLeftRelease, CaptureTrace Capture, bool? HookInstalled = null,
        int? Win32Error = null, int? Strikes = null, bool? MissedRelease = null, int? OsButtons = null);

    private DiagnosticRecord Snapshot(string kind, string reason, bool wasPressed, bool staleFrame) => new(kind,
        ++_stopSequence, DateTimeOffset.UtcNow, typeof(WindowsLivePointerRelay).Assembly.ManifestModule.ModuleVersionId,
        reason, staleFrame, wasPressed, _state.IsPressed, _state.IsRequested, _state.IsEnabled, _suspended, _draining,
        _intercepting, FrameIsFresh(), _leftHeld, _hookButtons, _commands.HasPendingLeftRelease, _captureMonitor.Trace);

    private void RecordStop(string reason, bool wasPressed, bool staleFrame = false) =>
        Append(Snapshot("stop", reason, wasPressed, staleFrame));

    private void RecordHookReinstall(bool installed, int error, int strikes, bool missedRelease, bool wasPressed, int osButtons) =>
        Append(Snapshot("hook-reinstall", installed ? "입력 훅 재설치" : "입력 훅 재설치 실패", wasPressed, false) with
        {
            HookInstalled = installed, Win32Error = error, Strikes = strikes, MissedRelease = missedRelease, OsButtons = osButtons
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
