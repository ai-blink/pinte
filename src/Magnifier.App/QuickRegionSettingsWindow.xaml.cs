using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Magnifier.Core;

namespace Magnifier.App;

public partial class QuickRegionSettingsWindow : Window
{
    private static readonly RegionPreset[] Presets =
    [
        new("320 × 180 · 16:9", 320, 180), new("640 × 360 · 16:9", 640, 360),
        new("960 × 540 · 16:9", 960, 540), new("1280 × 720 · 16:9", 1280, 720),
        new("1600 × 900 · 16:9", 1600, 900), new("1920 × 1080 · 16:9", 1920, 1080),
        new("640 × 480 · 4:3", 640, 480), new("800 × 600 · 4:3", 800, 600),
        new("1024 × 768 · 4:3", 1024, 768), new("480 × 480 · 정사각형", 480, 480),
        new("720 × 720 · 정사각형", 720, 720), new("1080 × 1080 · 정사각형", 1080, 1080)
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

    public QuickRegionSettingsWindow(IScreenCapture capture, ScreenRegion availableBounds, ScreenRegion region, double? lockedAspectRatio)
    {
        InitializeComponent();
        _capture = capture;
        _availableBounds = availableBounds;
        _region = region;
        _aspectRatio = lockedAspectRatio is { } value && value > 0 ? value : 16d / 9d;
        _presets = Presets.Select(x => x with { IsAvailable = x.Width <= availableBounds.Width && x.Height <= availableBounds.Height }).ToArray();
        PresetBox.ItemsSource = _presets;
        RatioBox.ItemsSource = Ratios;
        LockBox.IsChecked = lockedAspectRatio is not null;
        RefreshControls();
    }

    public event Action<RegionSizingOptions>? SizingChanged;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _capture.SetWindowCaptureExclusion(new WindowInteropHelper(this).Handle, true);
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _capture.SetWindowCaptureExclusion(new WindowInteropHelper(this).Handle, false); }
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
            CurrentSizeText.Text = $"현재 원본 영역: {_region.Width} × {_region.Height} 물리 픽셀";
            PresetBox.SelectedItem = _presets.FirstOrDefault(x => x.Width == _region.Width && x.Height == _region.Height);
            RatioBox.SelectedItem = Ratios.OrderBy(x => Math.Abs(x.Value - _aspectRatio)).First();
            DirectionButton.IsEnabled = Math.Abs(_aspectRatio - 1) > 0.0001;
            DirectionButton.Content = _aspectRatio >= 1 ? "가로" : "세로";
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
