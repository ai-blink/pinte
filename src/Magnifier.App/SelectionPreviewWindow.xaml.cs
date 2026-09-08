using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SelectionPreviewWindow : Window
{
    public SelectionPreviewWindow()
    {
        InitializeComponent();
    }

    public void UpdateCapture(CapturedFrame frame, bool isLivePreview)
    {
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

    public void ShowCaptureFailure(ScreenRegion selectionRegion, string reason)
    {
        SelectionBoundsText.Text =
            $"X {selectionRegion.X} · Y {selectionRegion.Y} · {selectionRegion.Width} × {selectionRegion.Height}";
        CapturedImage.Source = null;
        CaptureStatusText.Text = $"캡처 실패: {reason}";
    }
}
