namespace Magnifier.Core;

public sealed record RelayStatus(bool IsEnabled, bool IsRelaying, bool IsPressed,
    bool WaitingForRelease, PreviewPoint Position, string Message, bool InputRequested = false);

public interface ILivePointerRelay : IAsyncDisposable
{
    event Action<RelayStatus>? StatusChanged;
    Task ConfigureAsync(LensViewport viewport, params nint[] overlayWindows);
    Task<bool> StartAsync();
    Task PauseAsync(string reason);
    Task StopAsync(string reason);
    void RefreshFrame();
}
