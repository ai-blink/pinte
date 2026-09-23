using System.Windows;
using Magnifier.Core;

namespace Magnifier.App;

public partial class MainWindow
{
    private PointerTimingProfile _timingProfile = PointerTimingProfile.Default;
    private string? _timingLoadError;
    private PointerTimingWindow? _timingWindow;

    // Production startup only: tests keep the relay's built-in baseline and never read the profile.
    private async void InitializePointerTiming((PointerTimingProfile Profile, string? Error) loaded)
    {
        (_timingProfile, _timingLoadError) = loaded;
        if (_timingProfile.Applied == PointerTimingSettings.Default && _timingProfile.Revision == 0) return;
        try { await _relay.ApplyTimingAsync(_timingProfile.Applied, _timingProfile.Revision); }
        catch (Exception exception)
        {
            _timingLoadError = $"저장된 입력 타이밍 적용 실패 · 기본값 사용: {exception.Message}";
            _timingProfile = PointerTimingProfile.Default;
        }
    }

    private string TimingSummary => _timingLoadError is not null
        ? $"{_timingProfile.Applied} · {_timingLoadError}"
        : _timingProfile.Applied == PointerTimingSettings.Default
            ? $"{_timingProfile.Applied} (기본값)" : _timingProfile.Applied.ToString();

    // Opened from Settings > 고급. No owner: the anchor (lens/editor) can hide while
    // the user compares values in the target app, and the panel must stay available.
    private void OpenTimingWindow(Window anchor)
    {
        if (_timingWindow is not null)
        {
            _timingWindow.Activate();
            return;
        }
        var work = SystemParameters.WorkArea;
        _timingWindow = new PointerTimingWindow(_relay, _timingProfile, _timingLoadError)
        {
            Left = Math.Clamp(anchor.Left + anchor.ActualWidth + 8, work.Left, Math.Max(work.Left, work.Right - 400)),
            Top = Math.Clamp(anchor.Top, work.Top, Math.Max(work.Top, work.Bottom - 480))
        };
        _timingLoadError = null;
        _timingWindow.ProfileChanged += profile => _timingProfile = profile;
        _timingWindow.Closed += (_, _) => _timingWindow = null;
        _timingWindow.Show();
    }
}
