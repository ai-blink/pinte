using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Magnifier.App.Localization;
using Magnifier.Core;

namespace Magnifier.App;

public partial class QuickRegionSettingsWindow : Window
{
    // (Width, Height, ratio label or null for the localized "square" word) — labels are
    // built per-instance so the current language applies (this array can't be static readonly).
    private static readonly (int Width, int Height, string? RatioLabel)[] PresetDefs =
    [
        (320, 180, "16:9"), (640, 360, "16:9"), (960, 540, "16:9"), (1280, 720, "16:9"),
        (1600, 900, "16:9"), (1920, 1080, "16:9"), (640, 480, "4:3"), (800, 600, "4:3"),
        (1024, 768, "4:3"), (480, 480, null), (720, 720, null), (1080, 1080, null)
    ];
    private static readonly AspectOption[] Ratios =
    [new("16:9", 16d / 9d), new("9:16", 9d / 16d), new("4:3", 4d / 3d),
        new("3:4", 3d / 4d), new("1:1", 1d)];

    private readonly IScreenCapture _capture;
    private readonly ScreenRegion _availableBounds;
    private readonly RegionPreset[] _presets;
    private ScreenRegion _region;
    private double _aspectRatio;
    private bool _syncing;
    private readonly bool _hideFromScreenCapture;
    private nint _windowHandle;
    private bool _captureExcluded;

    public QuickRegionSettingsWindow(IScreenCapture capture, ScreenRegion availableBounds, ScreenRegion region,
        double? lockedAspectRatio, bool hideFromScreenCapture = false)
    {
        InitializeComponent();
        _capture = capture;
        _availableBounds = availableBounds;
        _region = region;
        _hideFromScreenCapture = hideFromScreenCapture;
        _aspectRatio = lockedAspectRatio is { } value && value > 0 ? value : 16d / 9d;
        _presets = PresetDefs.Select(d => new RegionPreset(
                $"{d.Width} × {d.Height} · {d.RatioLabel ?? Loc.Instance["QuickRegion_Square"]}", d.Width, d.Height,
                d.Width <= availableBounds.Width && d.Height <= availableBounds.Height))
            .ToArray();
        PresetBox.ItemsSource = _presets;
        RatioBox.ItemsSource = Ratios;
        LockBox.IsChecked = lockedAspectRatio is not null;
        RefreshControls();
    }

    public event Action<RegionSizingOptions>? SizingChanged;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        _capture.SetWindowCaptureExclusion(_windowHandle, _hideFromScreenCapture);
        _captureExcluded = _hideFromScreenCapture;
    }

    protected override void OnClosed(EventArgs e)
    {
        try { if (_captureExcluded) _capture.SetWindowCaptureExclusion(_windowHandle, false); }
        catch { }
        base.OnClosed(e);
    }

    private void PresetBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || PresetBox.SelectedItem is not RegionPreset preset) return;
        _aspectRatio = preset.Width / (double)preset.Height;
        Apply(preset.Width, preset.Height);
    }

    private void RatioBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || RatioBox.SelectedItem is not AspectOption option) return;
        _aspectRatio = option.Value;
        LockBox.IsChecked = true;
        Apply(_region.Width, (int)Math.Round(_region.Width / _aspectRatio));
    }

    private void LockBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncing) Apply(_region.Width, _region.Height);
    }

    private void DirectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Math.Abs(_aspectRatio - 1) < 0.0001) return;
        _aspectRatio = 1 / _aspectRatio;
        Apply(_region.Height, _region.Width);
    }

    private void Apply(int width, int height)
    {
        if (LockBox.IsChecked == true)
        {
            if (width > 0) height = (int)Math.Round(width / _aspectRatio);
        }
        if (LockBox.IsChecked == true)
        {
            var maximumWidth = Math.Min(_availableBounds.Width, (int)Math.Floor(_availableBounds.Height * _aspectRatio));
            width = Math.Clamp(width, Math.Min(80, maximumWidth), maximumWidth);
            height = Math.Max(1, (int)Math.Round(width / _aspectRatio));
        }
        _region = ScreenRegionSizing.Fit(new ScreenRegion(_region.X, _region.Y,
            Math.Max(1, width), Math.Max(1, height)), _availableBounds);
        SizingChanged?.Invoke(new RegionSizingOptions(_region.Width, _region.Height,
            LockBox.IsChecked == true ? _aspectRatio : null));
        RefreshControls();
    }

    private void RefreshControls()
    {
        _syncing = true;
        try
        {
            CurrentSizeText.Text = string.Format(Loc.Instance["QuickRegion_CurrentSize_Format"], _region.Width, _region.Height);
            PresetBox.SelectedItem = _presets.FirstOrDefault(x => x.Width == _region.Width && x.Height == _region.Height);
            RatioBox.SelectedItem = Ratios.OrderBy(x => Math.Abs(x.Value - _aspectRatio)).First();
            DirectionButton.IsEnabled = Math.Abs(_aspectRatio - 1) > 0.0001;
            DirectionButton.Content = Loc.Instance[_aspectRatio >= 1 ? "QuickRegion_Direction_Horizontal" : "QuickRegion_Direction_Vertical"];
        }
        finally { _syncing = false; }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void QuickRegionSettingsWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private sealed record RegionPreset(string Label, int Width, int Height, bool IsAvailable = true)
    {
        public override string ToString() => Label;
    }

    private sealed record AspectOption(string Label, double Value)
    {
        public override string ToString() => Label;
    }
}

public readonly record struct RegionSizingOptions(int Width, int Height, double? LockedAspectRatio);
