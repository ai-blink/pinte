using System.Windows.Threading;
using Magnifier.Core;

namespace Magnifier.App.Tests;

internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan elapsed) => _now += elapsed;
}

// 전달된 명령만 기록한다. Windows relay 엔진의 release/freshness를 재구현하지 않는다.
internal sealed class RecordingRelay : ILivePointerRelay
{
    public event Action<RelayStatus>? StatusChanged;
#pragma warning disable CS0067 // Timing status is not exercised by the App window tests.
    public event Action<PointerTimingStatus>? TimingChanged;
#pragma warning restore CS0067
    public PointerTimingStatus TimingStatus { get; private set; } = new(PointerTimingSettings.Default, 0, null, 0);
    public Task ApplyTimingAsync(PointerTimingSettings timing, long revision)
    {
        Calls.Add("timing");
        TimingStatus = new(timing, revision, null, 0);
        return Task.CompletedTask;
    }
    public List<bool> Suspensions { get; } = [];
    public List<string> Calls { get; } = [];
    public List<LensViewport> Configurations { get; } = [];
    public Func<Task>? PauseCallback { get; set; }
    public Exception? ConfigureFailure { get; set; }
    public Exception? SuspensionFailure { get; set; }
    public Exception? StopFailure { get; set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }
    public int FrameCount { get; private set; }

    public Task ConfigureAsync(LensViewport viewport, params nint[] overlayWindows)
    {
        Calls.Add("configure");
        Configurations.Add(viewport);
        return ConfigureFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }
    public Task<bool> StartAsync() { StartCount++; Calls.Add("start"); return Task.FromResult(true); }
    public Task PauseAsync(string reason)
    {
        Calls.Add("pause");
        return PauseCallback?.Invoke() ?? Task.CompletedTask;
    }
    public Task SetSuspendedAsync(bool suspended, string reason)
    {
        Calls.Add(suspended ? "suspend" : "unsuspend");
        Suspensions.Add(suspended);
        return SuspensionFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }
    public Task StopAsync(string reason)
    {
        StopCount++;
        Calls.Add("stop");
        return StopFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }
    public void RefreshFrame() { FrameCount++; Calls.Add("frame"); }
    public void Publish(RelayStatus status) => StatusChanged?.Invoke(status);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class NoDesktopCapture : IScreenCapture
{
    public int CaptureCount { get; private set; }
    public int ExclusionCount { get; private set; }
    public List<bool> ExclusionRequests { get; } = [];
    public Exception? ExclusionFailure { get; set; }
    public CapturedFrame Capture(ScreenRegion region)
    {
        CaptureCount++;
        return new CapturedFrame(region, new byte[checked(region.Width * region.Height * 4)]);
    }
    public void SetWindowCaptureExclusion(nint windowHandle, bool excludeFromCapture)
    {
        ExclusionCount++;
        ExclusionRequests.Add(excludeFromCapture);
        if (ExclusionFailure is { } failure) throw failure;
    }
}

internal sealed class NoDesktopWindows : IWindowEnvironment
{
    public ScreenRegion DesktopBounds => new(-1920, -1080, 3840, 2160);
    public int NativeOperationCount { get; private set; }
    public List<ScreenRegion> PlacedBounds { get; } = [];
    public List<ScreenRegion> TopmostPlacedBounds { get; } = [];
    public ScreenRegion WindowBounds { get; private set; } = new(100, 100, 800, 640);
    public ScreenRegion GetWindowBounds(nint handle) { NativeOperationCount++; return WindowBounds; }
    public ScreenRegion GetWindowWorkArea(nint handle) { NativeOperationCount++; return DesktopBounds; }
    public void PlaceWindow(nint handle, ScreenRegion bounds)
    {
        NativeOperationCount++;
        WindowBounds = bounds;
        PlacedBounds.Add(bounds);
    }
    public void PlaceTopmostWindow(nint handle, ScreenRegion bounds)
    {
        NativeOperationCount++;
        WindowBounds = bounds;
        TopmostPlacedBounds.Add(bounds);
    }
    public void SetPassiveOverlay(nint handle) => NativeOperationCount++;
    public bool IsRegionVisible(ScreenRegion region) => true;
}

internal sealed class NoDesktopPointer : IPointerInput
{
    public int CallCount { get; private set; }
    public void MoveTo(ScreenPoint point) => CallCount++;
    public void LeftButtonDown() => CallCount++;
    public void LeftButtonUp() => CallCount++;
}

internal sealed class HiddenWindows : IDisposable
{
    public RecordingRelay Relay { get; } = new();
    public NoDesktopCapture Capture { get; } = new();
    public NoDesktopWindows Windows { get; } = new();
    public NoDesktopPointer Pointer { get; } = new();
    public MainWindow Main { get; }
    public SelectionOverlayWindow Editor { get; }
    public SelectionPreviewWindow Lens { get; }

    public HiddenWindows()
    {
        Main = new MainWindow(Capture, Windows, Pointer, Relay, MagnifierSettings.Default, new ManualClock());
        Lens = new SelectionPreviewWindow(Relay, Capture, Windows, Pointer);
        Editor = new SelectionOverlayWindow(Capture, Windows);
        PrivateAccess.Set(Main, "_lens", Lens);
        PrivateAccess.Set(Main, "_frame", Editor);
    }

    public void Dispose()
    {
        PrivateAccess.Get<DispatcherTimer>(Main, "_timer").Stop();
        PrivateAccess.Get<DispatcherTimer>(Main, "_indicatorTimer").Stop();
        // 본체 Closing의 실제 복귀/Show 흐름은 테스트 종료에서 실행하지 않는다.
        PrivateAccess.Set(Main, "_closed", true);
        Main.Close();
        Lens.Close();
        Editor.Close();
    }
}
