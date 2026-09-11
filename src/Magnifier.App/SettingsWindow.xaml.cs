using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Magnifier.Core;

namespace Magnifier.App;

public partial class SettingsWindow : Window
{
    private static readonly SizeOption[] Sizes =
    [new(320, 180), new(640, 360), new(960, 540), new(1280, 720), new(1600, 900), new(1920, 1080)];
    private static readonly double[] Zooms = [1, 1.5, 2, 2.5, 3, 3.5, 4];
    private MagnifierSettings _settings;
    private readonly IScreenCapture _capture;
    private bool _syncing;

    public SettingsWindow(MagnifierSettings settings, IScreenCapture capture)
    {
        InitializeComponent();
        _settings = settings;
        _capture = capture;
        DefaultSizeBox.ItemsSource = Sizes;
        DefaultZoomBox.ItemsSource = Zooms;
        VersionText.Text = $"버전 {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "개발 빌드"}";
        RefreshControls();
        ShowPage(AppearancePanel);
    }

    public event Action<MagnifierSettings>? SettingsChanged;

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

    private void RefreshControls()
    {
        _syncing = true;
        try
        {
            TopToolbarButton.IsChecked = _settings.ToolbarPlacement == ToolbarPlacement.Top;
            BottomToolbarButton.IsChecked = _settings.ToolbarPlacement == ToolbarPlacement.Bottom;
            ThemeBox.SelectedIndex = (int)_settings.Theme;
            RememberLayoutBox.IsChecked = _settings.RememberLayout;
            DefaultSizeBox.SelectedItem = Sizes.FirstOrDefault(x => x.Width == _settings.DefaultSourceWidth && x.Height == _settings.DefaultSourceHeight);
            DefaultZoomBox.SelectedItem = Zooms.OrderBy(x => Math.Abs(x - _settings.DefaultZoom)).First();
            DefaultSizeBox.IsEnabled = DefaultZoomBox.IsEnabled = !_settings.RememberLayout;
        }
        finally { _syncing = false; }
    }

    private void ToolbarPlacement_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        Update(_settings with { ToolbarPlacement = BottomToolbarButton.IsChecked == true ? ToolbarPlacement.Bottom : ToolbarPlacement.Top });
    }

    private void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ThemeBox.SelectedIndex < 0) return;
        Update(_settings with { Theme = (ThemePreference)ThemeBox.SelectedIndex });
    }

    private void RememberLayout_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        Update(_settings with { RememberLayout = RememberLayoutBox.IsChecked == true });
    }

    private void DefaultSizeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || DefaultSizeBox.SelectedItem is not SizeOption size) return;
        Update(_settings with { DefaultSourceWidth = size.Width, DefaultSourceHeight = size.Height });
    }

    private void DefaultZoomBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || DefaultZoomBox.SelectedItem is not double zoom) return;
        Update(_settings with { DefaultZoom = zoom });
    }

    private void Update(MagnifierSettings settings)
    {
        _settings = settings;
        RefreshControls();
        SettingsChanged?.Invoke(_settings);
    }

    private void AppearancePage_OnClick(object sender, RoutedEventArgs e) => ShowPage(AppearancePanel);
    private void DefaultsPage_OnClick(object sender, RoutedEventArgs e) => ShowPage(DefaultsPanel);
    private void AboutPage_OnClick(object sender, RoutedEventArgs e) => ShowPage(AboutPanel);

    private void ShowPage(FrameworkElement page)
    {
        AppearancePanel.Visibility = page == AppearancePanel ? Visibility.Visible : Visibility.Collapsed;
        DefaultsPanel.Visibility = page == DefaultsPanel ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = page == AboutPanel ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenDiagnostics_OnClick(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magnifier", "diagnostics");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void SettingsWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private sealed record SizeOption(int Width, int Height)
    {
        public override string ToString() => $"{Width} × {Height}";
    }
}
