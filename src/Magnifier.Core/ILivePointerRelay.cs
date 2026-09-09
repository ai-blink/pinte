namespace Magnifier.Core;

public sealed record RelayStatus(bool IsEnabled, bool IsRelaying, bool IsPressed,
    bool WaitingForRelease, PreviewPoint Position, string Message);

public interface ILivePointerRelay : IAsyncDisposable
{
    event Action<RelayStatus>? StatusChanged;
    Task ConfigureAsync(LensViewport viewport, params nint[] overlayWindows);
    Task<bool> StartAsync();
    Task StopAsync(string reason);
    void RefreshFrame();
}
