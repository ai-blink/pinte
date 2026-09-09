using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow : Window
{
    private const int MinimumCountdownSeconds = 1;
    private const int MaximumCountdownSeconds = 10;
    private readonly PointerInputSession _inputSession;
    private readonly IScreenCapture _screenCapture;
    private nint _windowHandle;
    private ScreenRegion? _currentRegion;
    private CancellationTokenSource? _straightStrokeCancellation;
    private ScreenPoint? _pointA;
    private ScreenPoint? _pointB;
    private PreviewPoint? _pointAVisual;
    private PreviewPoint? _pointBVisual;
    private PointTarget _pendingPointTarget;
    private int _countdownSeconds = 3;
    private bool _isSynchronizingInputToggle;

    public SelectionPreviewWindow(IPointerInput pointerInput, IScreenCapture screenCapture)
    {
        InitializeComponent();
        _inputSession = new PointerInputSession(pointerInput);
        _screenCapture = screenCapture;
        UpdateStrokeControls();
    }

    public event Action<string>? InputStatusChanged;

    public bool IsInputEnabled => _inputSession.IsInputEnabled;

    public void UpdateCapture(CapturedFrame frame, bool isLivePreview)
    {
        if (_currentRegion is ScreenRegion currentRegion && currentRegion != frame.Region)
        {
            _pointA = null;
            _pointB = null;
            _pointAVisual = null;
            _pointBVisual = null;
            _pendingPointTarget = PointTarget.None;
            UpdateStrokeControls();
        }

        _currentRegion = frame.Region;
        SelectionBoundsText.Text =
            $"X {frame.Region.X} · Y {frame.Region.Y} · {frame.Region.Width} × {frame.Region.Height}";
        var bitmap = BitmapSource.Create(
            frame.Region.Width,
            frame.Region.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            frame.Bgra32Pixels.ToArray(),
            frame.Stride);
        bitmap.Freeze();

        CapturedImage.Source = bitmap;
        UpdatePointMarkers();
        CaptureStatusText.Text = isLivePreview
            ? "실시간 화면 캡처 · 최대 10fps"
            : "실제 화면 캡처";
    }

    public bool SetInputEnabled(bool isEnabled)
    {
        if (!isEnabled)
        {
            CancelStraightStroke();
        }

        try
        {
            _inputSession.SetInputEnabled(isEnabled);
            SetPreviewInputToggle(isEnabled);
            UpdateStrokeControls();
            PublishInputStatus(isEnabled ? "실제 포인터 입력: 켜짐" : "실제 포인터 입력: 꺼짐");
            return true;
        }
        catch (Exception exception)
        {
            PublishInputStatus($"입력 해제 실패: {exception.Message}");
            return false;
        }
    }

    private void PreviewInputEnabledCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingInputToggle)
        {
            return;
        }

        SetInputEnabled(PreviewInputEnabledCheckBox.IsChecked == true);
    }

    private void SetPreviewInputToggle(bool isEnabled)
    {
        if (PreviewInputEnabledCheckBox.IsChecked == isEnabled)
        {
            return;
        }

        _isSynchronizingInputToggle = true;
        try
        {
            PreviewInputEnabledCheckBox.IsChecked = isEnabled;
        }
        finally
        {
            _isSynchronizingInputToggle = false;
        }
    }

    public void CancelInputSession()
    {
        var cancelledCountdown = CancelStraightStroke();
        if (!_inputSession.IsPressed)
        {
            if (cancelledCountdown)
            {
                PublishInputStatus("A→B 카운트다운을 취소했습니다");
            }

            return;
        }

        try
        {
            _inputSession.Cancel();
            PublishInputStatus("입력 세션을 취소하고 포인터를 해제했습니다");
        }
        catch (Exception exception)
        {
            PublishInputStatus($"입력 해제 실패: {exception.Message}");
        }
        finally
        {
            ReleaseImageMouseCapture();
        }
    }

    public void ShowCaptureFailure(ScreenRegion selectionRegion, string reason)
    {
        SelectionBoundsText.Text =
            $"X {selectionRegion.X} · Y {selectionRegion.Y} · {selectionRegion.Width} × {selectionRegion.Height}";
        CapturedImage.Source = null;
        CaptureStatusText.Text = $"캡처 실패: {reason}";
    }

    private void CapturedImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var imagePoint = e.GetPosition(CapturedImage);

        if (_pendingPointTarget != PointTarget.None)
        {
            SelectPointFromImage(imagePoint);
            return;
        }

        PublishInputStatus(_inputSession.IsInputEnabled
            ? "A 지정 또는 B 지정 후 이미지를 클릭하세요 · 실제 입력은 A→B 실행 버튼에서만 보냅니다"
            : "A 지정 또는 B 지정 후 이미지를 클릭하세요 · 실제 포인터 입력: 꺼짐");
    }

    private void CapturedImage_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePointMarkers();
    }

    private void SelectionPreviewWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CancelInputSession();
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_windowHandle != nint.Zero)
        {
            _screenCapture.SetWindowCaptureExclusion(_windowHandle, excludeFromCapture: false);
        }

        CancelInputSession();
        base.OnClosed(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        _screenCapture.SetWindowCaptureExclusion(_windowHandle, excludeFromCapture: true);
    }

    private void CountdownDecrease_OnClick(object sender, RoutedEventArgs e)
    {
        _countdownSeconds = Math.Max(MinimumCountdownSeconds, _countdownSeconds - 1);
        UpdateStrokeControls();
        PublishInputStatus($"A→B 시작 대기: {_countdownSeconds}초");
    }

    private void CountdownIncrease_OnClick(object sender, RoutedEventArgs e)
    {
        _countdownSeconds = Math.Min(MaximumCountdownSeconds, _countdownSeconds + 1);
        UpdateStrokeControls();
        PublishInputStatus($"A→B 시작 대기: {_countdownSeconds}초");
    }

    private void SelectPointA_OnClick(object sender, RoutedEventArgs e)
    {
        BeginPointSelection(PointTarget.A);
    }

    private void SelectPointB_OnClick(object sender, RoutedEventArgs e)
    {
        BeginPointSelection(PointTarget.B);
    }

    private async void RunStraightStroke_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_inputSession.IsInputEnabled)
        {
            PublishInputStatus("실제 포인터 입력: 꺼짐 · A→B 실행은 checkbox를 켠 뒤에만 가능합니다");
            return;
        }

        if (_pointA is not ScreenPoint pointA || _pointB is not ScreenPoint pointB)
        {
            PublishInputStatus("A와 B를 모두 지정해야 합니다");
            return;
        }

        CancelStraightStroke();
        var cancellation = new CancellationTokenSource();
        _straightStrokeCancellation = cancellation;
        UpdateStrokeControls();

        try
        {
            for (var remainingSeconds = _countdownSeconds; remainingSeconds > 0; remainingSeconds--)
            {
                PublishInputStatus($"{remainingSeconds}초 뒤 A→B 직선 획 · Esc로 취소");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
            }

            cancellation.Token.ThrowIfCancellationRequested();
            DeliverStraightStrokeToVisibleScreen(pointA, pointB);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            PublishInputStatus("A→B 카운트다운을 취소했습니다");
        }
        catch (Exception exception)
        {
            HandleInputFailure(exception);
        }
        finally
        {
            if (ReferenceEquals(_straightStrokeCancellation, cancellation))
            {
                _straightStrokeCancellation = null;
                UpdateStrokeControls();
            }

            cancellation.Dispose();
        }
    }

    private void DeliverStraightStrokeToVisibleScreen(ScreenPoint pointA, ScreenPoint pointB)
    {
        // SendInput uses global screen coordinates. Hide this preview while it runs so the
        // selected visible screen area, rather than this window, receives the stroke.
        Hide();

        try
        {
            if (!_inputSession.Begin(pointA))
            {
                PublishInputStatus("실제 포인터 입력: 꺼짐 · A→B 실행을 시작하지 않았습니다");
                return;
            }

            _inputSession.Complete(pointB);
            PublishInputStatus("A→B 단일 직선 획 완료 · 포인터를 해제했습니다");
        }
        finally
        {
            Show();
        }
    }

    private bool TryMapToScreen(Point point, out ScreenPoint screenPoint, out PreviewPoint visualPoint)
    {
        if (_currentRegion is not ScreenRegion region || !TryGetRenderedImageBounds(region, out var renderedBounds))
        {
            screenPoint = default;
            visualPoint = default;
            return false;
        }

        var mappedPoint = new PreviewPoint(point.X - renderedBounds.X, point.Y - renderedBounds.Y);
        if (mappedPoint.X < 0 || mappedPoint.Y < 0 || mappedPoint.X > renderedBounds.Width || mappedPoint.Y > renderedBounds.Height)
        {
            screenPoint = default;
            visualPoint = default;
            return false;
        }

        visualPoint = new PreviewPoint(
            mappedPoint.X / renderedBounds.Width,
            mappedPoint.Y / renderedBounds.Height);
        screenPoint = PreviewCoordinateMapper.MapToScreen(
            region,
            new PreviewSize(renderedBounds.Width, renderedBounds.Height),
            mappedPoint);
        return true;
    }

    private void HandleInputFailure(Exception exception)
    {
        CancelStraightStroke();
        try
        {
            _inputSession.SetInputEnabled(false);
        }
        catch
        {
            // 세션은 release를 한 번 시도했고, 아래 상태 문구로 실패를 알린다.
        }
        finally
        {
            ReleaseImageMouseCapture();
        }

        PublishInputStatus($"입력 전달 실패: {exception.Message} · 입력 전달을 껐습니다");
    }

    private void PublishInputStatus(string status)
    {
        InputStatusText.Text = status;
        InputStatusChanged?.Invoke(status);
    }

    private void ReleaseImageMouseCapture()
    {
        if (CapturedImage.IsMouseCaptured)
        {
            CapturedImage.ReleaseMouseCapture();
        }
    }

    private void BeginPointSelection(PointTarget target)
    {
        _pendingPointTarget = target;
        UpdateStrokeControls();
        PublishInputStatus($"미리보기 이미지에서 {target} 지점을 선택하세요 · 실제 입력은 보내지 않습니다");
    }

    private void SelectPointFromImage(Point imagePoint)
    {
        if (!TryMapToScreen(imagePoint, out var point, out var visualPoint))
        {
            PublishInputStatus("이미지가 표시된 범위 안에서만 A/B를 지정할 수 있습니다");
            return;
        }

        if (_pendingPointTarget == PointTarget.A)
        {
            _pointA = point;
            _pointAVisual = visualPoint;
        }
        else
        {
            _pointB = point;
            _pointBVisual = visualPoint;
        }

        var selectedTarget = _pendingPointTarget;
        _pendingPointTarget = PointTarget.None;
        UpdateStrokeControls();
        UpdatePointMarkers();
        PublishInputStatus($"{selectedTarget} 지점을 X {point.X} · Y {point.Y}로 지정했습니다");
    }

    private bool CancelStraightStroke()
    {
        if (_straightStrokeCancellation is null)
        {
            return false;
        }

        _straightStrokeCancellation.Cancel();
        return true;
    }

    private void UpdateStrokeControls()
    {
        CountdownText.Text = $"{_countdownSeconds}초";
        PointStatusText.Text = $"A {FormatPoint(_pointA)} · B {FormatPoint(_pointB)}";
        RunStraightStrokeButton.IsEnabled =
            _inputSession.IsInputEnabled
            && _pointA.HasValue
            && _pointB.HasValue
            && _straightStrokeCancellation is null;
    }

    private static string FormatPoint(ScreenPoint? point)
    {
        return point is ScreenPoint value ? $"({value.X}, {value.Y})" : "미지정";
    }

    private enum PointTarget
    {
        None,
        A,
        B
    }
}
