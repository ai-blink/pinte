using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow : Window
{
    private readonly PointerInputSession _inputSession;
    private ScreenRegion? _currentRegion;

    public SelectionPreviewWindow(IPointerInput pointerInput)
    {
        InitializeComponent();
        _inputSession = new PointerInputSession(pointerInput);
    }

    public event Action<string>? InputStatusChanged;

    public bool IsInputEnabled => _inputSession.IsInputEnabled;

    public void UpdateCapture(CapturedFrame frame, bool isLivePreview)
    {
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
        CaptureStatusText.Text = isLivePreview
            ? "실시간 화면 캡처 · 최대 10fps"
            : "실제 화면 캡처";
    }

    public bool SetInputEnabled(bool isEnabled)
    {
        try
        {
            _inputSession.SetInputEnabled(isEnabled);
            PublishInputStatus(isEnabled ? "실제 포인터 입력: 켜짐" : "실제 포인터 입력: 꺼짐");
            return true;
        }
        catch (Exception exception)
        {
            PublishInputStatus($"입력 해제 실패: {exception.Message}");
            return false;
        }
    }

    public void CancelInputSession()
    {
        if (!_inputSession.IsPressed)
        {
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

        if (!_inputSession.IsInputEnabled)
        {
            PublishInputStatus("실제 포인터 입력: 꺼짐 · checkbox를 켜야 드래그를 전달합니다");
            return;
        }

        if (!TryMapToScreen(e.GetPosition(CapturedImage), out var point))
        {
            PublishInputStatus("이미지가 표시된 범위 안에서만 드래그를 시작할 수 있습니다");
            return;
        }

        if (!CapturedImage.CaptureMouse())
        {
            PublishInputStatus("미리보기 mouse capture를 시작하지 못했습니다");
            return;
        }

        try
        {
            if (_inputSession.Begin(point))
            {
                PublishInputStatus("입력 세션 진행 중 · Esc 또는 mouse capture 손실 시 해제합니다");
            }
            else
            {
                ReleaseImageMouseCapture();
            }
        }
        catch (Exception exception)
        {
            HandleInputFailure(exception);
        }
    }

    private void CapturedImage_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_inputSession.IsPressed || !CapturedImage.IsMouseCaptured)
        {
            return;
        }

        if (!TryMapToScreen(e.GetPosition(CapturedImage), out var point))
        {
            return;
        }

        try
        {
            _inputSession.Move(point);
        }
        catch (Exception exception)
        {
            HandleInputFailure(exception);
        }
    }

    private void CapturedImage_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!CapturedImage.IsMouseCaptured)
        {
            return;
        }

        e.Handled = true;

        try
        {
            if (TryMapToScreen(e.GetPosition(CapturedImage), out var point))
            {
                _inputSession.Complete(point);
                PublishInputStatus("입력 세션 완료 · 포인터를 해제했습니다");
            }
            else
            {
                CancelInputSession();
            }
        }
        catch (Exception exception)
        {
            HandleInputFailure(exception);
        }
        finally
        {
            ReleaseImageMouseCapture();
        }
    }

    private void CapturedImage_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        CancelInputSession();
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
        CancelInputSession();
        base.OnClosed(e);
    }

    private bool TryMapToScreen(Point point, out ScreenPoint screenPoint)
    {
        if (_currentRegion is not ScreenRegion region || CapturedImage.ActualWidth <= 0 || CapturedImage.ActualHeight <= 0)
        {
            screenPoint = default;
            return false;
        }

        var regionRatio = (double)region.Width / region.Height;
        var imageRatio = CapturedImage.ActualWidth / CapturedImage.ActualHeight;
        var renderedWidth = CapturedImage.ActualWidth;
        var renderedHeight = CapturedImage.ActualHeight;
        var offsetX = 0d;
        var offsetY = 0d;

        if (imageRatio > regionRatio)
        {
            renderedWidth = renderedHeight * regionRatio;
            offsetX = (CapturedImage.ActualWidth - renderedWidth) / 2;
        }
        else
        {
            renderedHeight = renderedWidth / regionRatio;
            offsetY = (CapturedImage.ActualHeight - renderedHeight) / 2;
        }

        var mappedPoint = new PreviewPoint(point.X - offsetX, point.Y - offsetY);
        if (mappedPoint.X < 0 || mappedPoint.Y < 0 || mappedPoint.X > renderedWidth || mappedPoint.Y > renderedHeight)
        {
            screenPoint = default;
            return false;
        }

        screenPoint = PreviewCoordinateMapper.MapToScreen(
            region,
            new PreviewSize(renderedWidth, renderedHeight),
            mappedPoint);
        return true;
    }

    private void HandleInputFailure(Exception exception)
    {
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
}
