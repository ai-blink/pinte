using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow : Window
{
    private readonly ILivePointerRelay _relay;
    private readonly IScreenCapture _screenCapture;
    private readonly IWindowEnvironment _windows;
    private readonly PointerInputSession _inputSession;
    private readonly DispatcherTimer _statusTimer;
    private ScreenRegion? _currentRegion;
    private nint _frameHandle;
    private WriteableBitmap? _bitmap;
    private RelayStatus? _relayStatus;
    private RelayStatus? _pendingRelayStatus;
    private bool _captureExcluded, _closed, _isMoving, _captureStopPending;
    private int _sourceRevision, _inputRequestRevision;
    private string? _captureFailureReason;
    private bool _editingAllowed = true;
    private bool _externalInteractionLocked;

    public SelectionPreviewWindow(ILivePointerRelay relay, IScreenCapture capture,
        IWindowEnvironment windows, IPointerInput pointer)
    {
        _relay = relay;
        _screenCapture = capture;
        _windows = windows;
        _inputSession = new PointerInputSession(pointer);
        InitializeComponent();
        _statusTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _statusTimer.Tick += (_, _) => ApplyPendingRelayStatus();
        _statusTimer.Start();
        _relay.StatusChanged += Relay_OnStatusChanged;
        Loaded += (_, _) => QueueGeometryUpdate();
        LocationChanged += (_, _) => { if (IsLoaded && !_sizing) QueueGeometryUpdate(); };
        SizeChanged += (_, _) => QueueGeometryUpdate();
        InitializeResizeControls();
        UpdateControls();
    }

    public event Action? ReturnRequested;
    public event Action<bool>? EditingAllowedChanged;
    public event Action<string>? InputStatusChanged;
    public event Action? PlacementChanged;
    public event Action? RegionSettingsRequested;
    public event Action? AppSettingsRequested;
    public event Action? SourceEditRequested;
    public event Action<ScreenPoint>? LensHideRequested;
    public event Action? GeometryInvalidated;
    public nint WindowHandle { get; private set; }
    public ScreenRegion? CurrentRegion => _currentRegion;
    public double Zoom { get; private set; } = 2;

    public bool IsInteractionLocked => _externalInteractionLocked || _relayStatus is { IsPressed: true } or { WaitingForRelease: true };

    public async Task SetSourceAsync(ScreenRegion source, nint frameHandle)
    {
        if (_currentRegion == source && _frameHandle == frameHandle) return;
        var revision = ++_sourceRevision;
        await PauseAsync("영역 변경 · 조작 자동 재개 대기");
        if (_closed || revision != _sourceRevision) return;
        _frameHandle = frameHandle;
        _currentRegion = source;
        _captureFailureReason = null;
        ClearPoints();
        _bitmap = null;
        CapturedImage.Source = null;
        EmptyImageText.Visibility = Visibility.Visible;
        EmptyImageText.Text = "원본 화면을 기다리는 중입니다";
        UpdateBoundsText(source);
        RequestViewportCentering();
        ResizeLens();
        await Dispatcher.Yield(DispatcherPriority.Loaded);
        await ConfigureGeometryAsync();
        UpdateControls();
    }

    public void UpdateCapture(CapturedFrame frame, bool isLivePreview)
    {
        if (_closed || _captureStopPending ||
            (_currentRegion is null && _relayStatus is { IsPressed: true })) return;
        // 이미지 표시가 먼저 있어야 WPF Image의 실제 크기를 측정할 수 있다.
        // 좌표 대기는 표시를 막지 않고, relay의 새 프레임 인정만 보류한다.
        if (_currentRegion is null)
        {
            _currentRegion = frame.Region;
            UpdateBoundsText(frame.Region);
            RequestViewportCentering();
            ResizeLens();
            QueueGeometryUpdate();
        }
        if (_currentRegion != frame.Region) return;
        _captureFailureReason = null;
        if (_bitmap is null || _bitmap.PixelWidth != frame.Region.Width || _bitmap.PixelHeight != frame.Region.Height)
        {
            // BitBlt의 BI_RGB 32비트 출력은 BGRX다. Bgr32로 표시하면 매 프레임
            // 알파 채널을 따로 채울 필요가 없어 장시간 캡처의 CPU 부하를 줄인다.
            _bitmap = new WriteableBitmap(frame.Region.Width, frame.Region.Height, 96, 96, PixelFormats.Bgr32, null);
            CapturedImage.Source = _bitmap;
            QueueGeometryUpdate();
        }
        var pixels = MemoryMarshal.TryGetArray(frame.Bgra32Pixels, out ArraySegment<byte> buffer)
            ? buffer : new ArraySegment<byte>(frame.Bgra32Pixels.ToArray());
        _bitmap.WritePixels(new Int32Rect(0, 0, frame.Region.Width, frame.Region.Height),
            pixels.Array!, frame.Stride, pixels.Offset);
        EmptyImageText.Visibility = Visibility.Collapsed;
        if (!_geometryPending) _relay.RefreshFrame();
        CaptureStatusText.Text = isLivePreview
            ? "실시간 화면 · 가장자리를 벗어나면 해제 · 대상 반응을 확인하세요"
            : "화면 캡처 · 실제 입력 꺼짐";
        UpdateControls();
    }

    public async Task StopAsync(string reason)
    {
        _inputRequestRevision++;
        _resumeAfterAuxiliary = false;
        CancelStraightStroke();
        Exception? auxiliaryFailure = null;
        try { _inputSession.SetInputEnabled(false); }
        catch (Exception exception) { auxiliaryFailure = exception; reason += $" · A/B 해제 실패: {exception.Message}"; }
        SetPreviewInputToggle(false);
        try { await _relay.StopAsync(reason); }
        catch (Exception exception) { PublishInputStatus($"입력 중지 실패: {exception.Message}"); throw; }
        _handToolEnabled = false;
        EndPan();
        _isMoving = _moveReady = _isResizing = _resizeReady = false;
        ++_resizeRevision;
        if (Mouse.Captured is Thumb thumb &&
            (thumb == TitleThumb || thumb == CompactMoveThumb || ResizeLayer.Children.Contains(thumb))) thumb.CancelDrag();
        ApplyPanModeUi();
        await ApplyInputSuspensionAsync(reason);
        UpdateControls();
        if (auxiliaryFailure is not null) throw new InvalidOperationException(reason, auxiliaryFailure);
    }

    public async Task PauseAsync(string reason)
    {
        try { await _relay.PauseAsync(reason); }
        catch (Exception exception) { PublishInputStatus($"입력 일시 정지 실패: {exception.Message}"); throw; }
    }

    public async Task SetInputSuspendedAsync(bool suspended, string reason)
    {
        _externalInputSuspended = suspended;
        await ApplyInputSuspensionAsync(reason);
    }

    public void SetInteractionLocked(bool locked)
    {
        if (_externalInteractionLocked == locked) return;
        _externalInteractionLocked = locked;
        UpdateControls();
    }

    public void SetToolbarPlacement(ToolbarPlacement placement)
    {
        var top = placement == ToolbarPlacement.Top;
        TopToolbarRow.Height = top ? GridLength.Auto : new GridLength(0);
        BottomToolbarRow.Height = top ? new GridLength(0) : GridLength.Auto;
        Grid.SetRow(Toolbar, top ? 1 : 4);
        QueueGeometryUpdate();
    }

    public async Task StartInputAsync()
    {
        if (_closed || !IsVisible || !_captureExcluded || AuxiliaryTools.IsExpanded || _handToolEnabled ||
            _sourceEditing || _isResizing || _currentRegion is null || _captureFailureReason is not null) return;
        var revision = ++_inputRequestRevision;
        await ConfigureGeometryAsync();
        if (revision != _inputRequestRevision || _closed || !IsVisible || AuxiliaryTools.IsExpanded ||
            _sourceEditing || _handToolEnabled || _isResizing || _currentRegion is null || _captureFailureReason is not null) return;
        await _relay.StartAsync();
    }

    public async Task ShowCaptureFailureAsync(ScreenRegion region, string reason)
    {
        if (_currentRegion is null && _captureFailureReason == reason) return;
        _captureFailureReason = reason;
        _currentRegion = null;
        _bitmap = null;
        CapturedImage.Source = null;
        ClearPoints();
        EmptyImageText.Text = $"캡처 실패: {reason}";
        EmptyImageText.Visibility = Visibility.Visible;
        UpdateBoundsText(region);
        CaptureStatusText.Text = "캡처를 자동 재시도합니다 · 새 화면을 받으면 조작을 자동 재개합니다";
        UpdateControls();
        _captureStopPending = true;
        try { await PauseAsync($"캡처 일시 실패 · 새 화면 대기: {reason}"); }
        catch { /* A release failure cancels the pending request in the relay. */ }
        finally { _captureStopPending = false; }
    }

    private async void Return_OnClick(object sender, RoutedEventArgs e)
    {
        await ReturnToOriginalScreenAsync();
    }

    private async void CloseLens_OnClick(object sender, RoutedEventArgs e)
    {
        await ReturnToOriginalScreenAsync();
        e.Handled = true;
    }

    private async Task ReturnToOriginalScreenAsync()
    {
        try
        {
            await StopAsync("원래 화면 · 입력 해제");
            ReturnRequested?.Invoke();
        }
        catch { }
    }

    private void Relay_OnStatusChanged(RelayStatus status)
    {
        // The hook thread only stores the latest value. UI cost is bounded to 62.5 Hz.
        Interlocked.Exchange(ref _pendingRelayStatus, status);
    }

    private void ApplyPendingRelayStatus()
    {
        if (_closed || Interlocked.Exchange(ref _pendingRelayStatus, null) is not { } status) return;
        try
        {
            _relayStatus = status;
            PublishInputStatus(status.Message);
            VirtualCursor.Visibility = status.IsRelaying && status.IsPressed ? Visibility.Visible : Visibility.Collapsed;
            if (status.IsRelaying && CapturedImage.IsVisible)
            {
                var imagePoint = CapturedImage.PointFromScreen(new Point(status.Position.X, status.Position.Y));
                var point = CapturedImage.TranslatePoint(imagePoint, PointMarkerLayer);
                Canvas.SetLeft(VirtualCursor, point.X - 13);
                Canvas.SetTop(VirtualCursor, point.Y - 13);
            }
            UpdateControls();
        }
        catch (InvalidOperationException)
        {
            // The visual may disconnect during a return/close. Never propagate to the hook.
            VirtualCursor.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateControls()
    {
        if (!IsInitialized) return;
        var manipulationLocked = _externalInteractionLocked || _relayStatus is { IsPressed: true } or { WaitingForRelease: true }
            || _straightStrokeCancellation is not null;
        var locked = manipulationLocked;
        var editingAllowed = !manipulationLocked && !_sourceEditing;
        TitleThumb.IsEnabled = editingAllowed;
        ZoomDecreaseButton.IsEnabled = editingAllowed && _currentRegion.HasValue;
        ZoomIncreaseButton.IsEnabled = ZoomDecreaseButton.IsEnabled;
        FineZoomSlider.IsEnabled = ZoomDecreaseButton.IsEnabled;
        FineZoomText.IsEnabled = ZoomDecreaseButton.IsEnabled;
        PanModeButton.IsEnabled = editingAllowed && _currentRegion.HasValue && !AuxiliaryTools.IsExpanded;
        RegionSettingsButton.IsEnabled = editingAllowed && _currentRegion.HasValue;
        AppSettingsButton.IsEnabled = !manipulationLocked;
        SourceEditButton.IsEnabled = editingAllowed && _currentRegion.HasValue;
        HideLensButton.IsEnabled = editingAllowed && _currentRegion.HasValue && !_isResizing && !_isMoving;
        UpdateCompactControls();
        AuxiliaryTools.IsEnabled = editingAllowed || AuxiliaryTools.IsExpanded;
        AuxiliaryControls.IsEnabled = !locked;
        if (_editingAllowed != editingAllowed)
        {
            _editingAllowed = editingAllowed;
            EditingAllowedChanged?.Invoke(editingAllowed);
        }
        UpdateResizeControlState();
        UpdateStrokeControls();
    }

    private void PublishInputStatus(string status)
    {
        InputStatusText.Text = status;
        InputStatusText.ToolTip = status;
        InputStatusChanged?.Invoke(status);
    }

    private void UpdateBoundsText(ScreenRegion region) => SelectionBoundsText.Text =
        $"원본 물리 좌표 X {region.X} · Y {region.Y} · {region.Width} × {region.Height} · 렌즈 이동과 독립";

    private void CapturedImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_pendingPointTarget != PointTarget.None) SelectPointFromImage(e.GetPosition(CapturedImage));
        else PublishInputStatus(AuxiliaryTools.IsExpanded ? "A 또는 B 지점 지정을 먼저 누르세요"
            : _relayStatus is { InputRequested: true } ? "버튼 해제와 최신 화면을 기다린 뒤 자동 재개합니다" : "원래 화면으로 돌아간 뒤 다시 확대하세요");
    }

    private void RegionSettings_OnClick(object sender, RoutedEventArgs e)
    {
        if (_editingAllowed)
        {
            RegionSettingsRequested?.Invoke();
        }
        e.Handled = true;
    }

    private void AppSettings_OnClick(object sender, RoutedEventArgs e)
    {
        if (AppSettingsButton.IsEnabled)
        {
            AppSettingsRequested?.Invoke();
        }
        e.Handled = true;
    }

    private void HideLens_OnClick(object sender, RoutedEventArgs e)
    {
        if (HideLensButton.IsEnabled && CompactHideLensButton.IsEnabled)
        {
            var button = (FrameworkElement)sender;
            var anchor = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            LensHideRequested?.Invoke(new ScreenPoint((int)Math.Round(anchor.X), (int)Math.Round(anchor.Y)));
        }
        e.Handled = true;
    }

    public void ShowInteractionNotice(string message) => PublishInputStatus(message);

    private void CapturedImage_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePointMarkers();
        QueueGeometryUpdate();
    }

    private async void TitleThumb_OnDragStarted(object sender, DragStartedEventArgs e)
    {
        _isMoving = _editingAllowed;
        if (!_isMoving) return;
        _moveReady = false;
        try { await ApplyInputSuspensionAsync("렌즈 이동 · 버튼 해제 후 자동 재개"); _moveReady = _isMoving; }
        catch { _isMoving = false; ((Thumb)sender).CancelDrag(); }
    }
    private void TitleThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_isMoving || !_moveReady || !_editingAllowed) return;
        Left += e.HorizontalChange;
        Top += e.VerticalChange;
    }
    private async void TitleThumb_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        await FinishMoveAsync();
    }
    private async void TitleThumb_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isMoving) return;
        await FinishMoveAsync();
    }

    private void SelectionPreviewWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        // The relay's global low-level keyboard hook distinguishes a physical Escape from a
        // virtual keyboard's injected Escape. WPF does not expose that provenance, so it must
        // only consume this focused key and leave cancellation to the Infrastructure boundary.
        e.Handled = true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowHandle = new WindowInteropHelper(this).Handle;
        try
        {
            _screenCapture.SetWindowCaptureExclusion(WindowHandle, true);
            _captureExcluded = true;
        }
        catch (Exception exception) { PublishInputStatus($"렌즈 캡처 제외 실패 · 조작 불가: {exception.Message}"); }
        UpdateControls();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        StopGeometryRetry();
        _statusTimer.Stop();
        _relay.StatusChanged -= Relay_OnStatusChanged;
        try { if (_captureExcluded) _screenCapture.SetWindowCaptureExclusion(WindowHandle, false); }
        catch { }
        base.OnClosed(e);
    }
}
