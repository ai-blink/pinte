namespace Magnifier.Core;

public sealed record RelayStatus(bool IsEnabled, bool IsRelaying, bool IsPressed,
    bool WaitingForRelease, PreviewPoint Position, string Message, bool InputRequested = false);

public interface ILivePointerRelay : IAsyncDisposable
{
    event Action<RelayStatus>? StatusChanged;
    event Action<PointerTimingStatus>? TimingChanged;
    PointerTimingStatus TimingStatus { get; }
    // Applies at the next idle sequence boundary; an in-flight gesture keeps its snapshot.
    Task ApplyTimingAsync(PointerTimingSettings timing, long revision);
    Task ConfigureAsync(LensViewport viewport, params nint[] overlayWindows);
    Task<bool> StartAsync();
    Task PauseAsync(string reason);
    Task SetSuspendedAsync(bool suspended, string reason);
    Task StopAsync(string reason);
    void RefreshFrame();
}
